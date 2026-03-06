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
    private readonly ConcurrentDictionary<IPEndPoint, byte> _activeDatagramEndpoints = new();
    private readonly SocketServerState _socketServerState;
    private readonly SocketServerConfig _configuration;
    private Semaphore _semaphore;
    private readonly CancellationTokenSource _cancellationTokenSource = new();
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
        _semaphore = new Semaphore(maxConnections, maxConnections);
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
            socketServer.NoDelay = endpointConfig.NagleAlgorithm;
            socketServer.LingerState = new LingerOption(endpointConfig.LingerTimeSeconds.HasValue,
                endpointConfig.LingerTimeSeconds.GetValueOrDefault());
        }
    }

    public void Start()
    {
        // Bind + Listen in all ports (Unless it is in Udp - cannot listen for connections in Udp)
        var nonUdpSocketsCount = _socketServers.Count(socket => socket.Value.ProtocolType != ProtocolType.Udp);
        var connectionsAcceptanceSlots = nonUdpSocketsCount == 0
            ? 0
            : Math.Max(1, _configuration.ConnectionAcceptanceValue / nonUdpSocketsCount);
        foreach (var (endpoint, socket) in _socketServers)
        {
            ExposeServer(endpoint, socket.ProtocolType == ProtocolType.Udp ? 0 : connectionsAcceptanceSlots);
        }

        _logger.LogInformation("Socket server started, Listening for incoming connections in {SocketServersEndpoints}",
            string.Join(", ", _socketServers.Keys));

        // Accept new connections - then Broadcast + Collect task run TODO - add handling both
        while (!_cancellationTokenSource.IsCancellationRequested)
        {
            foreach (var endpoint in _socketServers.Keys)
            {
                if (!TryBeginEndpointProcessing(endpoint))
                    continue;

                _semaphore.WaitOne();
                _ = Task.Run(() => ProcessChannel(GetAcceptedClientChannelAsync(endpoint), endpoint));
            }
        }
    }

    private async Task AwaitTriggerToActivateEndpointAction(IPEndPoint endpoint)
    {
        while (!_socketServerState.IsEndpointPortActionEnabled(endpoint.Port))
        {
            await Task.Delay(5);
        }
    }

    /// <summary>
    /// Processes the Socket channel communications each asynchronously.
    /// </summary>
    /// <param name="task">Task representing Socket channel.</param>
    /// <param name="endpoint">The endpoint to perform methods by.</param>
    /// <exception cref="ArgumentException">Raised when no method is resolved to perform for the endpoint.</exception>
    private async Task ProcessChannel(Task<Socket> task, IPEndPoint endpoint)
    {
        Socket? channel = null;
        try
        {
            channel = await task;
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
            await _cancellationTokenSource.CancelAsync();
            _logger.LogCritical(exception,
                "Encountered critical Socket server communication error, shutting down server.");
            Environment.Exit(1);
        }
        finally
        {
            CompleteEndpointProcessing(endpoint);
            channel?.DisposeIfRequired(_socketServers[endpoint]);
            _semaphore.Release();
        }
    }

    /// <summary>
    /// Task to return socket channel created on socket connection handshake. If endpoint is set for Udp
    /// socket - connection channel is based on the socket's bound-channel.
    /// </summary>
    /// <param name="endpoint">The ipv4 endpoint to accept clients connections to.</param>
    /// <returns>Task representing Socket channel to communicate on.</returns>
    private Task<Socket> GetAcceptedClientChannelAsync(IPEndPoint endpoint)
    {
        return Task.Run(async () =>
        {
            if (_socketServers[endpoint].ProtocolType == ProtocolType.Udp)
                return _socketServers[endpoint];
            var channel = await _socketServers[endpoint].AcceptAsync();
            _logger.LogInformation(
                "Local endpoint - {LocalEndPoint} accepted connection from remote endpoint {RemoteEndPoint} to handle method {SocketMethod}",
                endpoint, channel.RemoteEndPoint, ResolveSocketMethod(endpoint));
            return channel;
        });
    }

    private async Task HandleBroadcast(Socket socket, IPEndPoint localEndpoint)
    {
        var dataToBroadcast = _socketServerState.Process(localEndpoint.Port);

        foreach (var data in dataToBroadcast.Select(data => data.CastObjectData<byte[]>()))
        {
            try
            {
                await SendAllAsync(data.Body ?? [], payload => socket.SendAsync(payload, SocketFlags.None));
            }
            catch (Exception exception)
            {
                _logger.LogError(
                    "Received exception broadcasting message to channel {ChannelEndPoint}, error - {Exception}",
                    socket.RemoteEndPoint, exception);
            }
        }
    }

    private async Task HandleCollect(Socket socket, IPEndPoint localEndpoint)
    {
        var endpointConfiguration = _configuration.Endpoints!.First(config => config.Port == localEndpoint.Port);
        var collectedData = Collect(socket, endpointConfiguration.TimeoutMs.GetValueOrDefault(),
            endpointConfiguration.BufferSizeBytes, localEndpoint);
        _socketServerState
            .Process(localEndpoint.Port, collectedData
                .Select(bytes => new Data<object> { Body = bytes }))
            .ToArray();
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
        try
        {
            socketServer.Listen(connectionsAcceptanceSlots);
        }
        catch (SocketException)
        {
            _logger.LogWarning(
                "Error while listening at {Endpoint} - configured 'Socket' in protocol {ProtocolType} can't support clients listen backlog option",
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

    internal bool TryBeginEndpointProcessing(IPEndPoint endpoint)
    {
        return _socketServers[endpoint].ProtocolType != ProtocolType.Udp ||
               _activeDatagramEndpoints.TryAdd(endpoint, 0);
    }

    internal void CompleteEndpointProcessing(IPEndPoint endpoint)
    {
        if (_socketServers[endpoint].ProtocolType == ProtocolType.Udp)
            _activeDatagramEndpoints.TryRemove(endpoint, out _);
    }

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
}

internal static class SocketDisposalExtensions
{
    internal static void DisposeIfRequired(this Socket channel, Socket serverSocket)
    {
        if (!ReferenceEquals(channel, serverSocket))
            channel.Dispose();
    }
}
