using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.IO;
using System.Net;
using System.Net.Sockets;
using Microsoft.Extensions.Logging;
using QaaS.Framework.SDK.DataSourceObjects;
using QaaS.Framework.SDK.Extensions;
using QaaS.Framework.SDK.Session.DataObjects;
using QaaS.Mocker.Servers.ConfigurationObjects.SocketServerConfigs;
using QaaS.Mocker.Servers.Extensions;
using QaaS.Mocker.Servers.ServerStates;
using QaaS.Mocker.Stubs.Stubs;

namespace QaaS.Mocker.Servers.Servers;

/// <summary>
/// Server implementation for Socket communications that runs Broadcast and Collect mechanisms.
/// </summary>
public class SocketServer : IServer
{
    private readonly ILogger _logger;
    private readonly Dictionary<IPEndPoint, Socket> _socketServers = new();
    private readonly ConcurrentDictionary<Task, byte> _activeProcessingTasks = new();
    // UDP endpoints share the bound server socket, so only one receive/send loop may own them at a time.
    private readonly ConcurrentDictionary<IPEndPoint, byte> _activeDatagramEndpoints = new();
    private readonly SocketServerState _socketServerState;
    private readonly SocketServerConfig _configuration;
    private SemaphoreSlim _semaphore;
    private readonly CancellationTokenSource _cancellationTokenSource = new();
    private readonly Lock _fatalExceptionLock = new();
    private Exception? _fatalException;
    public IServerState State { get; init; }

    /// <summary>
    /// Initializes a new instance of the <see cref="SocketServer"/> class.
    /// </summary>
    /// <param name="socketServerConfig">Configuration for the Socket server.</param>
    /// <param name="logger">Logger for logging server activities</param>
    /// <param name="transactionStubList">List of transaction stubs to handle data transportation in server</param>
    /// <param name="dataSourceList">List of data sources to use in broadcast per endpoint</param>
    public SocketServer(SocketServerConfig socketServerConfig, ILogger logger,
        IImmutableList<TransactionStub> transactionStubList, IImmutableList<DataSource> dataSourceList)
    {
        _logger = logger;
        _configuration = socketServerConfig;
        var endpoints = socketServerConfig.Endpoints!;
        foreach (var endpoint in endpoints)
        {
            // Validate combinations here as well so invalid configs fail fast even when configuration
            // validation is bypassed and the server is created directly in tests or tooling.
            if (endpoint.ProtocolType == ProtocolType.Udp && endpoint.Action?.Method == SocketMethod.Broadcast)
                throw new NotSupportedException(
                    $"Socket endpoint on port {endpoint.Port} cannot use Broadcast with UDP.");
            if (endpoint.ProtocolType == ProtocolType.Udp && endpoint.SocketType != SocketType.Dgram)
                throw new NotSupportedException(
                    $"Socket endpoint on port {endpoint.Port} must use SocketType.Dgram with UDP.");
            if (endpoint.ProtocolType == ProtocolType.Tcp && endpoint.SocketType != SocketType.Stream)
                throw new NotSupportedException(
                    $"Socket endpoint on port {endpoint.Port} must use SocketType.Stream with TCP.");

            var ipEndpoint =
                new IPEndPoint(IPAddress.Parse(socketServerConfig.BindingIpAddress), endpoint.Port!.Value);
            _socketServers[ipEndpoint] = new Socket(addressFamily: endpoint.AddressFamily,
                socketType: endpoint.SocketType,
                protocolType: endpoint.ProtocolType!.Value);
            ConstructSocketServerFromConfiguration(ipEndpoint, endpoint);
            _logger.LogInformation(
                "Registered socket endpoint '{Endpoint}' with protocol '{ProtocolType}', socket type '{SocketType}', action '{ActionName}', method '{SocketMethod}', timeout {TimeoutMs} ms, and buffer size {BufferSizeBytes} bytes",
                ipEndpoint,
                endpoint.ProtocolType,
                endpoint.SocketType,
                endpoint.Action?.Name ?? "<unnamed>",
                endpoint.Action?.Method,
                endpoint.TimeoutMs,
                endpoint.BufferSizeBytes);
        }

        _socketServerState = new SocketServerState(
            logger,
            dataSourceList,
            transactionStubList,
            socketServerConfig.Endpoints!
        );
        State = _socketServerState;
        InitializeSemaphore(socketServerConfig.ConnectionAcceptanceValue);
    }

    /// <summary>
    /// Initializes the semaphore used to control the number of concurrent connections.
    /// </summary>
    /// <param name="connectionAcceptanceValue">Base value for connection acceptance.</param>
    private void InitializeSemaphore(int connectionAcceptanceValue)
    {
        var maxConnections = Environment.ProcessorCount * connectionAcceptanceValue;
        _semaphore = new SemaphoreSlim(maxConnections, maxConnections);
        _logger.LogDebug("Connection Acceptance Semaphore initialized with max connections of {MaxConnections}",
            maxConnections);
    }

    /// <summary>
    /// Constructs socket's properties according to configured values in the given endpoint.
    /// </summary>
    /// <param name="endpoint">The ipv4 endpoint to initiate.</param>
    /// <param name="endpointConfig">The endpoint configuration to configure socket by.</param>
    private void ConstructSocketServerFromConfiguration(IPEndPoint endpoint, SocketEndpointConfig endpointConfig)
    {
        if (!_socketServers.TryGetValue(endpoint, out var socketServer))
            throw new ArgumentException(
                $"Could not initialize 'Socket' server in binding address {endpoint} - Instance of endpoint not found.");

        socketServer.SendBufferSize = endpointConfig.BufferSizeBytes;
        socketServer.ReceiveBufferSize = endpointConfig.BufferSizeBytes;
        socketServer.SendTimeout = endpointConfig.TimeoutMs.GetValueOrDefault();
        socketServer.ReceiveTimeout = endpointConfig.TimeoutMs.GetValueOrDefault();
        if (endpointConfig.ProtocolType == ProtocolType.Tcp)
        {
            socketServer.NoDelay = !endpointConfig.NagleAlgorithm;
            socketServer.LingerState = new LingerOption(endpointConfig.LingerTimeSeconds.HasValue,
                endpointConfig.LingerTimeSeconds.GetValueOrDefault());
        }
        _logger.LogDebug(
            "Configured socket endpoint '{Endpoint}' with send/receive buffer {BufferSizeBytes} bytes, timeout {TimeoutMs} ms, Nagle disabled: {NagleDisabled}, linger seconds: {LingerTimeSeconds}",
            endpoint,
            endpointConfig.BufferSizeBytes,
            endpointConfig.TimeoutMs,
            endpointConfig.ProtocolType == ProtocolType.Tcp && !endpointConfig.NagleAlgorithm,
            endpointConfig.LingerTimeSeconds?.ToString() ?? "<none>");
    }

    public void Start()
    {
        // TCP endpoints split the configured backlog, while UDP endpoints are bind-only and use zero here.
        var nonUdpSocketsCount = _socketServers.Count(socket => socket.Value.ProtocolType != ProtocolType.Udp);
        var connectionsAcceptanceSlots = nonUdpSocketsCount == 0
            ? 0
            : Math.Max(1, _configuration.ConnectionAcceptanceValue / nonUdpSocketsCount);
        foreach (var (endpoint, socket) in _socketServers)
        {
            ExposeServer(endpoint, socket.ProtocolType == ProtocolType.Udp ? 0 : connectionsAcceptanceSlots);
        }

        _logger.LogInformation(
            "Socket server started with {EndpointCount} endpoint(s). Max concurrent connections: {MaxConnections}. Endpoints: {SocketServersEndpoints}",
            _socketServers.Count,
            Environment.ProcessorCount * _configuration.ConnectionAcceptanceValue,
            string.Join(", ", _socketServers.Keys));

        var endpointTasks = _socketServers.Keys.Select(RunEndpointLoopAsync).ToArray();
        try
        {
            Task.WhenAll(endpointTasks).GetAwaiter().GetResult();
            Task.WhenAll(_activeProcessingTasks.Keys).GetAwaiter().GetResult();
        }
        catch (OperationCanceledException) when (_cancellationTokenSource.IsCancellationRequested)
        {
            if (_fatalException == null)
                _logger.LogInformation("Socket server processing loop was canceled.");
        }

        if (_fatalException != null)
        {
            throw new IOException("Socket server stopped because a fatal processing error was encountered.",
                _fatalException);
        }
    }

    private Task RunEndpointLoopAsync(IPEndPoint endpoint)
    {
        return _socketServers[endpoint].ProtocolType == ProtocolType.Udp
            ? RunDatagramEndpointLoopAsync(endpoint)
            : RunTcpEndpointLoopAsync(endpoint);
    }

    private async Task RunDatagramEndpointLoopAsync(IPEndPoint endpoint)
    {
        while (!_cancellationTokenSource.IsCancellationRequested)
        {
            await _semaphore.WaitAsync(_cancellationTokenSource.Token);
            await ProcessChannel(_socketServers[endpoint], endpoint);
        }
    }

    private async Task RunTcpEndpointLoopAsync(IPEndPoint endpoint)
    {
        while (!_cancellationTokenSource.IsCancellationRequested)
        {
            await _semaphore.WaitAsync(_cancellationTokenSource.Token);
            try
            {
                var channel = await GetAcceptedClientChannelAsync(endpoint, _cancellationTokenSource.Token);
                TrackProcessingTask(ProcessChannel(channel, endpoint));
            }
            catch (OperationCanceledException) when (_cancellationTokenSource.IsCancellationRequested)
            {
                _semaphore.Release();
                break;
            }
            catch
            {
                _semaphore.Release();
                throw;
            }
        }
    }

    private async Task AwaitTriggerToActivateEndpointAction(IPEndPoint endpoint)
    {
        if (!_socketServerState.IsEndpointPortActionEnabled(endpoint.Port))
        {
            _logger.LogDebug(
                "Socket endpoint '{Endpoint}' is waiting for action '{ActionName}' to be enabled",
                endpoint, ResolveActionName(endpoint));
        }
        while (!_socketServerState.IsEndpointPortActionEnabled(endpoint.Port))
        {
            await Task.Delay(5, _cancellationTokenSource.Token);
        }
        _logger.LogDebug(
            "Socket endpoint '{Endpoint}' action '{ActionName}' is enabled and ready to process",
            endpoint, ResolveActionName(endpoint));
    }

    /// <summary>
    /// Processes the Socket channel communications each asynchronously.
    /// </summary>
    /// <param name="channel">Accepted socket channel or shared datagram socket.</param>
    /// <param name="endpoint">The endpoint to perform methods by.</param>
    /// <exception cref="ArgumentException">Raised when no method is resolved to perform for the endpoint.</exception>
    private async Task ProcessChannel(Socket channel, IPEndPoint endpoint)
    {
        try
        {
            _logger.LogInformation(
                "Starting socket processing for endpoint '{Endpoint}' action '{ActionName}' using channel '{ChannelEndPoint}'",
                endpoint, ResolveActionName(endpoint), DescribeChannel(channel));
            await AwaitTriggerToActivateEndpointAction(endpoint);
            switch (ResolveSocketMethod(endpoint))
            {
                case SocketMethod.Broadcast:
                    await HandleBroadcast(channel, endpoint);
                    break;
                case SocketMethod.Collect:
                    await HandleCollect(channel, endpoint);
                    break;
            }
        }
        catch (Exception exception)
        {
            RecordFatalException(exception);
            await _cancellationTokenSource.CancelAsync();
            DisposeServerSockets();
            _logger.LogCritical(exception,
                "Encountered critical Socket server communication error, shutting down server.");
        }
        finally
        {
            CompleteEndpointProcessing(endpoint);
            // Accepted TCP channels must be disposed, but UDP processing reuses the bound server socket.
            channel?.DisposeIfRequired(_socketServers[endpoint]);
            _logger.LogDebug(
                "Completed socket processing for endpoint '{Endpoint}' action '{ActionName}'",
                endpoint, ResolveActionName(endpoint));
            _semaphore.Release();
        }
    }

    /// <summary>
    /// Task to return socket channel created on socket connection handshake. If endpoint is set for Udp
    /// socket - connection channel is based on the socket's bound-channel.
    /// </summary>
    /// <param name="endpoint">The ipv4 endpoint to accept clients connections to.</param>
    /// <param name="cancellationToken">Token used to stop pending accepts during shutdown.</param>
    /// <returns>Task representing Socket channel to communicate on.</returns>
    private async Task<Socket> GetAcceptedClientChannelAsync(IPEndPoint endpoint, CancellationToken cancellationToken)
    {
        if (_socketServers[endpoint].ProtocolType == ProtocolType.Udp)
            return _socketServers[endpoint];

        var channel = await _socketServers[endpoint].AcceptAsync(cancellationToken);
        _logger.LogInformation(
            "Socket endpoint '{LocalEndPoint}' accepted connection from '{RemoteEndPoint}' for action '{ActionName}' method '{SocketMethod}'",
            endpoint, channel.RemoteEndPoint, ResolveActionName(endpoint), ResolveSocketMethod(endpoint));
        return channel;
    }

    private async Task HandleBroadcast(Socket socket, IPEndPoint localEndpoint)
    {
        var dataToBroadcast = _socketServerState.Process(localEndpoint.Port);
        var messageCount = 0;
        var totalBytes = 0;

        foreach (var data in dataToBroadcast.Select(data => data.CastObjectData<byte[]>()))
        {
            try
            {
                var payload = data.Body ?? [];
                // Socket.SendAsync may complete with a partial write, so loop until the payload is exhausted.
                await SendAllAsync(payload, message => socket.SendAsync(message, SocketFlags.None));
                messageCount++;
                totalBytes += payload.Length;
                _logger.LogDebug(
                    "Broadcasted message {MessageNumber} for action '{ActionName}' on endpoint '{Endpoint}' to '{RemoteEndPoint}' ({PayloadLength} bytes)",
                    messageCount, ResolveActionName(localEndpoint), localEndpoint, DescribeChannel(socket), payload.Length);
            }
            catch (Exception exception)
            {
                _logger.LogError(exception,
                    "Failed to broadcast payload for action '{ActionName}' on endpoint '{Endpoint}' to channel '{ChannelEndPoint}'",
                    ResolveActionName(localEndpoint), localEndpoint, DescribeChannel(socket));
            }
        }

        _logger.LogInformation(
            "Completed broadcast for action '{ActionName}' on endpoint '{Endpoint}'. Sent {MessageCount} message(s) totaling {TotalBytes} bytes",
            ResolveActionName(localEndpoint), localEndpoint, messageCount, totalBytes);
    }

    private async Task HandleCollect(Socket socket, IPEndPoint localEndpoint)
    {
        var endpointConfiguration = _configuration.Endpoints!.First(config => config.Port == localEndpoint.Port);
        var collectedData = Collect(socket, endpointConfiguration.TimeoutMs.GetValueOrDefault(),
            endpointConfiguration.BufferSizeBytes, localEndpoint);
        var payloadCount = 0;
        var totalBytes = 0;
        _socketServerState
            .Process(localEndpoint.Port, collectedData
                .Select(bytes =>
                {
                    payloadCount++;
                    totalBytes += bytes.Length;
                    _logger.LogDebug(
                        "Collected payload {PayloadNumber} for action '{ActionName}' on endpoint '{Endpoint}' from '{ChannelEndPoint}' ({PayloadLength} bytes)",
                        payloadCount, ResolveActionName(localEndpoint), localEndpoint, DescribeChannel(socket), bytes.Length);
                    return new Data<object> { Body = bytes };
                }))
            .ToArray();
        _logger.LogInformation(
            "Completed collect for action '{ActionName}' on endpoint '{Endpoint}'. Received {PayloadCount} payload(s) totaling {TotalBytes} bytes",
            ResolveActionName(localEndpoint), localEndpoint, payloadCount, totalBytes);
    }

    /// <summary>
    /// Collects buffers received from Socket channel while buffer received has body
    /// </summary>
    private IEnumerable<byte[]> Collect(Socket socket, int timeoutMs, int bufferSizeBytes, IPEndPoint localEndpoint)
    {
        byte[]? bytes;
        do
        {
            bytes = socket.GetBytesFromChannelWithinTimeout(timeoutMs, bufferSizeBytes,
                socket.ProtocolType == ProtocolType.Udp ? localEndpoint : null,
                _logger);
            if (bytes == null)
                yield break;

            yield return bytes;
        } while (bytes is { Length: > 0 });
    }

    /// <summary>
    /// Binds and listens for configured amount of connections by endpoint
    /// </summary>
    /// <param name="endpoint">Endpoint of socket server to bind and listen for clients with</param>
    /// <param name="connectionsAcceptanceSlots">Amount of clients to handle in parallel</param>
    private void ExposeServer(IPEndPoint endpoint, int connectionsAcceptanceSlots)
    {
        if (!_socketServers.TryGetValue(endpoint, out var socketServer))
            throw new ArgumentException(
                $"Could not expose 'Socket' server in binding address {endpoint} - Instance of endpoint not found.");

        socketServer.Bind(endpoint);
        if (socketServer.ProtocolType == ProtocolType.Udp)
        {
            // UDP endpoints are ready immediately after bind; calling Listen would be both unnecessary
            // and noisy because many platforms report it as an unsupported operation.
            _logger.LogInformation(
                "Bound UDP socket endpoint '{Endpoint}' for action '{ActionName}'",
                endpoint, ResolveActionName(endpoint));
            return;
        }

        try
        {
            socketServer.Listen(connectionsAcceptanceSlots);
            _logger.LogInformation(
                "Listening on TCP socket endpoint '{Endpoint}' for action '{ActionName}' with backlog {Backlog}",
                endpoint, ResolveActionName(endpoint), connectionsAcceptanceSlots);
        }
        catch (SocketException)
        {
            _logger.LogWarning(
                "Socket endpoint '{Endpoint}' could not apply listen backlog for protocol '{ProtocolType}'",
                endpoint, _socketServers[endpoint].ProtocolType);
        }
    }

    /// <summary>
    /// Resolves socket method configured by socket's bound endpoint.
    /// </summary>
    /// <param name="endpoint">The ipv4 endpoint to resolve by.</param>
    /// <returns>The method type to execute in the given endpoint.</returns>
    private SocketMethod ResolveSocketMethod(IPEndPoint endpoint)
    {
        return _configuration.Endpoints!.First(config => config.Port == endpoint.Port).Action?.Method ??
               throw new ArgumentException($"Could not resolve socket method for endpoint {endpoint}");
    }

    private void TrackProcessingTask(Task task)
    {
        _activeProcessingTasks[task] = 0;
        _ = task.ContinueWith(completedTask => _activeProcessingTasks.TryRemove(completedTask, out _),
            TaskScheduler.Default);
    }

    private void RecordFatalException(Exception exception)
    {
        using (_fatalExceptionLock.EnterScope())
        {
            _fatalException ??= exception;
        }
    }

    private void DisposeServerSockets()
    {
        foreach (var socket in _socketServers.Values)
        {
            try
            {
                socket.Dispose();
            }
            catch (ObjectDisposedException)
            {
            }
        }
    }

    private string ResolveActionName(IPEndPoint endpoint)
    {
        return _configuration.Endpoints!.First(config => config.Port == endpoint.Port).Action?.Name ?? "<unnamed>";
    }

    /// <summary>
    /// Reserves an endpoint for processing. UDP endpoints are single-flight because they share one
    /// bound socket, while TCP endpoints can safely schedule overlapping accept loops.
    /// </summary>
    internal bool TryBeginEndpointProcessing(IPEndPoint endpoint)
    {
        return _socketServers[endpoint].ProtocolType != ProtocolType.Udp ||
               _activeDatagramEndpoints.TryAdd(endpoint, 0);
    }

    /// <summary>
    /// Releases the per-endpoint processing reservation created by <see cref="TryBeginEndpointProcessing"/>.
    /// </summary>
    internal void CompleteEndpointProcessing(IPEndPoint endpoint)
    {
        if (_socketServers[endpoint].ProtocolType == ProtocolType.Udp)
            _activeDatagramEndpoints.TryRemove(endpoint, out _);
    }

    /// <summary>
    /// Writes the full payload even when the underlying socket only accepts partial sends.
    /// </summary>
    internal static async Task SendAllAsync(ReadOnlyMemory<byte> payload,
        Func<ReadOnlyMemory<byte>, ValueTask<int>> sendAsync)
    {
        var bytesSent = 0;
        while (bytesSent < payload.Length)
        {
            var sent = await sendAsync(payload[bytesSent..]);
            if (sent <= 0)
                throw new IOException("Socket channel stopped sending before the payload was fully written.");

            bytesSent += sent;
        }
    }

    private static string DescribeChannel(Socket channel)
    {
        try
        {
            return channel.RemoteEndPoint?.ToString() ?? channel.LocalEndPoint?.ToString() ?? "<unresolved>";
        }
        catch (SocketException)
        {
            return channel.LocalEndPoint?.ToString() ?? "<unresolved>";
        }
        catch (ObjectDisposedException)
        {
            return "<disposed>";
        }
    }
}

/// <summary>
/// Socket cleanup helpers for accepted channels.
/// </summary>
internal static class SocketDisposalExtensions
{
    /// <summary>
    /// Disposes accepted client sockets while leaving the bound server socket alive.
    /// </summary>
    internal static void DisposeIfRequired(this Socket channel, Socket serverSocket)
    {
        if (!ReferenceEquals(channel, serverSocket))
            channel.Dispose();
    }
}
