using System.Collections.Immutable;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using NUnit.Framework;
using QaaS.Framework.SDK.DataSourceObjects;
using QaaS.Mocker.Servers.ConfigurationObjects.SocketServerConfigs;
using QaaS.Mocker.Servers.Servers;
using QaaS.Mocker.Stubs.Stubs;

namespace QaaS.Mocker.Servers.Tests;

[TestFixture]
public class SocketServerTests
{
    [Test]
    public void Constructor_WithUdpBroadcastEndpoint_ThrowsNotSupportedException()
    {
        var config = new SocketServerConfig
        {
            Endpoints =
            [
                new SocketEndpointConfig
                {
                    Port = 7001,
                    ProtocolType = ProtocolType.Udp,
                    SocketType = SocketType.Dgram,
                    TimeoutMs = 100,
                    Action = new SocketActionConfig
                    {
                        Name = "BroadcastA",
                        Method = SocketMethod.Broadcast,
                        DataSourceName = "ds1"
                    }
                }
            ]
        };

        Assert.Throws<NotSupportedException>(() =>
            new SocketServer(config, Globals.Logger, ImmutableList<TransactionStub>.Empty, ImmutableList<DataSource>.Empty));
    }

    [Test]
    public async Task SendAllAsync_WhenTransportReturnsPartialWrites_RetriesUntilPayloadComplete()
    {
        var payload = Encoding.UTF8.GetBytes("abcdef");
        var sentSegments = new List<string>();

        await SocketServer.SendAllAsync(payload, segment =>
        {
            sentSegments.Add(Encoding.UTF8.GetString(segment.Span));
            return new ValueTask<int>(Math.Min(2, segment.Length));
        });

        Assert.That(sentSegments, Is.EqualTo(new[] { "abcdef", "cdef", "ef" }));
    }

    [Test]
    public void SendAllAsync_WhenTransportStopsSending_ThrowsIOException()
    {
        var payload = Encoding.UTF8.GetBytes("abcdef");

        Assert.ThrowsAsync<IOException>(async () =>
            await SocketServer.SendAllAsync(payload, _ => new ValueTask<int>(0)));
    }

    [Test]
    public void TryBeginEndpointProcessing_WithUdpEndpoint_PreventsOverlappingProcessing()
    {
        var server = CreateServer(ProtocolType.Udp, SocketMethod.Collect);
        var endpoint = CreateEndpoint();

        Assert.Multiple(() =>
        {
            Assert.That(server.TryBeginEndpointProcessing(endpoint), Is.True);
            Assert.That(server.TryBeginEndpointProcessing(endpoint), Is.False);
        });

        server.CompleteEndpointProcessing(endpoint);

        Assert.That(server.TryBeginEndpointProcessing(endpoint), Is.True);
    }

    [Test]
    public void TryBeginEndpointProcessing_WithTcpEndpoint_AllowsRepeatedScheduling()
    {
        var server = CreateServer(ProtocolType.Tcp, SocketMethod.Collect);
        var endpoint = CreateEndpoint();

        Assert.Multiple(() =>
        {
            Assert.That(server.TryBeginEndpointProcessing(endpoint), Is.True);
            Assert.That(server.TryBeginEndpointProcessing(endpoint), Is.True);
        });
    }

    [Test]
    public void Constructor_WithTcpNagleEnabled_ConfiguresSocketNoDelayToFalse()
    {
        var server = new SocketServer(
            new SocketServerConfig
            {
                Endpoints =
                [
                    new SocketEndpointConfig
                    {
                        Port = 7001,
                        ProtocolType = ProtocolType.Tcp,
                        SocketType = SocketType.Stream,
                        TimeoutMs = 100,
                        NagleAlgorithm = true,
                        Action = new SocketActionConfig
                        {
                            Name = "CollectA",
                            Method = SocketMethod.Collect
                        }
                    }
                ]
            },
            Globals.Logger,
            ImmutableList<TransactionStub>.Empty,
            ImmutableList<DataSource>.Empty);

        var socketServers = (Dictionary<IPEndPoint, Socket>)typeof(SocketServer)
            .GetField("_socketServers", BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(server)!;

        Assert.That(socketServers.Single().Value.NoDelay, Is.False);
    }

    [Test]
    public void Constructor_WithUdpStreamSocket_ThrowsNotSupportedException()
    {
        var config = new SocketServerConfig
        {
            Endpoints =
            [
                new SocketEndpointConfig
                {
                    Port = 7001,
                    ProtocolType = ProtocolType.Udp,
                    SocketType = SocketType.Stream,
                    TimeoutMs = 100,
                    Action = new SocketActionConfig
                    {
                        Name = "CollectA",
                        Method = SocketMethod.Collect
                    }
                }
            ]
        };

        Assert.Throws<NotSupportedException>(() =>
            new SocketServer(config, Globals.Logger, ImmutableList<TransactionStub>.Empty, ImmutableList<DataSource>.Empty));
    }

    [Test]
    public void Constructor_WithTcpDatagramSocket_ThrowsNotSupportedException()
    {
        var config = new SocketServerConfig
        {
            Endpoints =
            [
                new SocketEndpointConfig
                {
                    Port = 7001,
                    ProtocolType = ProtocolType.Tcp,
                    SocketType = SocketType.Dgram,
                    TimeoutMs = 100,
                    Action = new SocketActionConfig
                    {
                        Name = "CollectA",
                        Method = SocketMethod.Collect
                    }
                }
            ]
        };

        Assert.Throws<NotSupportedException>(() =>
            new SocketServer(config, Globals.Logger, ImmutableList<TransactionStub>.Empty, ImmutableList<DataSource>.Empty));
    }

    [Test]
    public void ResolveSocketMethod_WhenActionIsMissing_ThrowsArgumentException()
    {
        var config = new SocketServerConfig
        {
            Endpoints =
            [
                new SocketEndpointConfig
                {
                    Port = 7001,
                    ProtocolType = ProtocolType.Tcp,
                    SocketType = SocketType.Stream,
                    TimeoutMs = 100,
                    Action = new SocketActionConfig
                    {
                        Name = "CollectA",
                        Method = SocketMethod.Collect
                    }
                }
            ]
        };
        var server = new SocketServer(
            config,
            Globals.Logger,
            ImmutableList<TransactionStub>.Empty,
            ImmutableList<DataSource>.Empty);
        config.Endpoints![0].Action = null;

        var exception = Assert.Throws<TargetInvocationException>(() =>
            typeof(SocketServer)
                .GetMethod("ResolveSocketMethod", BindingFlags.Instance | BindingFlags.NonPublic)!
                .Invoke(server, [CreateEndpoint()]));

        Assert.That(exception!.InnerException, Is.TypeOf<ArgumentException>());
    }

    [Test]
    public void ExposeServer_WhenEndpointIsUnknown_ThrowsArgumentException()
    {
        var server = CreateServer(ProtocolType.Tcp, SocketMethod.Collect);
        var unknownEndpoint = new IPEndPoint(IPAddress.Loopback, 7999);

        var exception = Assert.Throws<TargetInvocationException>(() =>
            typeof(SocketServer)
                .GetMethod("ExposeServer", BindingFlags.Instance | BindingFlags.NonPublic)!
                .Invoke(server, [unknownEndpoint, 1]));

        Assert.That(exception!.InnerException, Is.TypeOf<ArgumentException>());
    }

    [Test]
    public void DescribeChannel_WithDisposedSocket_ReturnsDisposedMarker()
    {
        var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        socket.Dispose();

        var description = (string)typeof(SocketServer)
            .GetMethod("DescribeChannel", BindingFlags.Static | BindingFlags.NonPublic)!
            .Invoke(null, [socket])!;

        Assert.That(description, Is.EqualTo("<disposed>"));
    }

    [Test]
    public void DisposeIfRequired_WithSameSocketReference_DoesNotDisposeServerSocket()
    {
        using var serverSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

        SocketDisposalExtensions.DisposeIfRequired(serverSocket, serverSocket);

        Assert.DoesNotThrow(() => serverSocket.Bind(new IPEndPoint(IPAddress.Loopback, 0)));
    }

    [Test]
    public void DisposeIfRequired_WithDifferentSocket_DisposesClientSocket()
    {
        using var serverSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        var clientSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

        SocketDisposalExtensions.DisposeIfRequired(clientSocket, serverSocket);

        Assert.Throws<ObjectDisposedException>(() => clientSocket.Bind(new IPEndPoint(IPAddress.Loopback, 0)));
    }

    [Test]
    public async Task GetAcceptedClientChannelAsync_WithUdpEndpoint_ReturnsBoundServerSocket()
    {
        var server = CreateServer(ProtocolType.Udp, SocketMethod.Collect);
        var endpoint = CreateEndpoint();
        var socketServers = (Dictionary<IPEndPoint, Socket>)typeof(SocketServer)
            .GetField("_socketServers", BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(server)!;

        var channel = await InvokeGetAcceptedClientChannelAsync(server, endpoint, CancellationToken.None);

        Assert.That(channel, Is.SameAs(socketServers[endpoint]));
    }

    [Test]
    public async Task AwaitTriggerToActivateEndpointAction_CompletesAfterActionIsEnabled()
    {
        var server = CreateServer(ProtocolType.Tcp, SocketMethod.Broadcast);
        var endpoint = CreateEndpoint();

        var waitTask = InvokeAwaitTriggerToActivateEndpointAction(server, endpoint);
        await Task.Delay(30);
        Assert.That(waitTask.IsCompleted, Is.False);

        server.State.TriggerAction("ActionA", 50);
        await waitTask;

        Assert.That(waitTask.IsCompletedSuccessfully, Is.True);
    }

    [Test]
    public async Task Collect_WithTcpSocket_YieldsReceivedPayload()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();

        using var client = new TcpClient();
        var connectTask = client.ConnectAsync(IPAddress.Loopback, ((IPEndPoint)listener.LocalEndpoint).Port);
        using var serverSocket = await listener.AcceptSocketAsync();
        await connectTask;

        await using (var clientStream = client.GetStream())
        {
            var payload = Encoding.UTF8.GetBytes("payload");
            await clientStream.WriteAsync(payload);
            await clientStream.FlushAsync();
        }

        var server = CreateServer(ProtocolType.Tcp, SocketMethod.Collect);
        var collected = InvokeCollect(server, serverSocket, 200, 1024, (IPEndPoint)listener.LocalEndpoint).ToArray();

        Assert.That(collected.Select(Encoding.UTF8.GetString), Is.EqualTo(new[] { "payload" }));
    }

    [Test]
    public async Task Collect_WhenSocketTimesOut_YieldsNoPayloads()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();

        using var client = new TcpClient();
        var connectTask = client.ConnectAsync(IPAddress.Loopback, ((IPEndPoint)listener.LocalEndpoint).Port);
        using var serverSocket = await listener.AcceptSocketAsync();
        await connectTask;

        var server = CreateServer(7001, ProtocolType.Tcp, SocketMethod.Collect);
        var collected = InvokeCollect(server, serverSocket, 50, 1024, CreateEndpoint(7001)).ToArray();

        Assert.That(collected, Is.Empty);
    }

    [Test]
    public async Task ProcessChannel_WithTcpCollectEndpoint_ProcessesIncomingPayload()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();

        using var client = new TcpClient();
        var connectTask = client.ConnectAsync(IPAddress.Loopback, ((IPEndPoint)listener.LocalEndpoint).Port);
        using var serverSocket = await listener.AcceptSocketAsync();
        await connectTask;

        await using (var clientStream = client.GetStream())
        {
            var payload = Encoding.UTF8.GetBytes("payload");
            await clientStream.WriteAsync(payload);
            await clientStream.FlushAsync();
        }

        var server = CreateServer(7001, ProtocolType.Tcp, SocketMethod.Collect);
        var cache = server.State.GetCache();
        cache.EnableStorage = true;
        ReserveProcessingSlot(server);

        await InvokeProcessChannel(server, serverSocket, CreateEndpoint(7001));

        Assert.Multiple(() =>
        {
            Assert.That(cache.RetrieveFirstOrDefaultStringInput(), Is.Not.Null);
            Assert.That(cache.RetrieveFirstOrDefaultStringOutput(), Is.Null);
        });
    }

    [Test]
    public async Task Start_WithTcpEndpointInvalidConfiguration_ThrowsArgumentException()
    {
        var port = GetFreeTcpPort();
        var config = CreateConfig(port, ProtocolType.Tcp, SocketMethod.Collect);
        var server = new SocketServer(config, Globals.Logger, ImmutableList<TransactionStub>.Empty, ImmutableList<DataSource>.Empty);
        config.Endpoints![0].Action = null;

        var startTask = Task.Run(() => server.Start());
        await Task.Delay(100);
        using var client = new TcpClient();
        await client.ConnectAsync(IPAddress.Loopback, port);

        var completedTask = await Task.WhenAny(startTask, Task.Delay(TimeSpan.FromSeconds(5)));
        Assert.That(completedTask, Is.SameAs(startTask));

        Assert.ThrowsAsync<ArgumentException>(async () => await startTask);
    }

    [Test]
    public async Task Start_WithUdpEndpointFatalProcessingError_ThrowsIOException()
    {
        var port = GetFreeUdpPort();
        var config = CreateConfig(port, ProtocolType.Udp, SocketMethod.Collect);
        var server = new SocketServer(config, Globals.Logger, ImmutableList<TransactionStub>.Empty, ImmutableList<DataSource>.Empty);
        config.Endpoints![0].Action = null;

        var startTask = Task.Run(() => server.Start());
        await Task.Delay(100);
        using var sender = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
        var payload = Encoding.UTF8.GetBytes("udp");
        sender.SendTo(payload, new IPEndPoint(IPAddress.Loopback, port));

        var completedTask = await Task.WhenAny(startTask, Task.Delay(TimeSpan.FromSeconds(5)));
        Assert.That(completedTask, Is.SameAs(startTask));

        var exception = Assert.ThrowsAsync<IOException>(async () => await startTask);
        Assert.That(exception!.InnerException, Is.Not.Null);
    }

    [Test]
    public async Task Start_WhenCancellationFollowsRecordedFatalException_ThrowsIOException()
    {
        var port = GetFreeTcpPort();
        var server = CreateServer(port, ProtocolType.Tcp, SocketMethod.Collect);
        var semaphore = (SemaphoreSlim)typeof(SocketServer)
            .GetField("_semaphore", BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(server)!;
        while (semaphore.Wait(0))
        {
        }

        var startTask = Task.Run(() => server.Start());
        await Task.Delay(100);

        typeof(SocketServer)
            .GetMethod("RecordFatalException", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(server, [new InvalidOperationException("boom")]);
        var cancellationTokenSource = (CancellationTokenSource)typeof(SocketServer)
            .GetField("_cancellationTokenSource", BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(server)!;
        await cancellationTokenSource.CancelAsync();

        var exception = Assert.ThrowsAsync<IOException>(async () => await startTask);

        Assert.That(exception!.InnerException, Is.TypeOf<InvalidOperationException>());
    }

    [Test]
    public void DisposeServerSockets_WithDisposedServerSocket_DoesNotThrow()
    {
        var server = CreateServer(7001, ProtocolType.Tcp, SocketMethod.Collect);
        var socketServers = (Dictionary<IPEndPoint, Socket>)typeof(SocketServer)
            .GetField("_socketServers", BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(server)!;
        socketServers.Single().Value.Dispose();

        Assert.DoesNotThrow(() => typeof(SocketServer)
            .GetMethod("DisposeServerSockets", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(server, null));
    }

    [Test]
    public void DescribeChannel_WithUnconnectedSocket_ReturnsLocalEndpoint()
    {
        using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        socket.Bind(new IPEndPoint(IPAddress.Loopback, 0));

        var description = (string)typeof(SocketServer)
            .GetMethod("DescribeChannel", BindingFlags.Static | BindingFlags.NonPublic)!
            .Invoke(null, [socket])!;

        Assert.That(description, Is.EqualTo(socket.LocalEndPoint!.ToString()));
    }

    [Test]
    public void DescribeChannel_WithNeverBoundSocket_ReturnsUnresolvedMarker()
    {
        using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

        var description = (string)typeof(SocketServer)
            .GetMethod("DescribeChannel", BindingFlags.Static | BindingFlags.NonPublic)!
            .Invoke(null, [socket])!;

        Assert.That(description, Is.EqualTo("<unresolved>"));
    }

    private static IPEndPoint CreateEndpoint() => CreateEndpoint(7001);

    private static IPEndPoint CreateEndpoint(int port) => new(IPAddress.Parse("0.0.0.0"), port);

    private static async Task<Socket> InvokeGetAcceptedClientChannelAsync(SocketServer server, IPEndPoint endpoint,
        CancellationToken cancellationToken)
    {
        var task = (Task<Socket>)typeof(SocketServer)
            .GetMethod("GetAcceptedClientChannelAsync", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(server, [endpoint, cancellationToken])!;

        return await task;
    }

    private static async Task InvokeAwaitTriggerToActivateEndpointAction(SocketServer server, IPEndPoint endpoint)
    {
        var task = (Task)typeof(SocketServer)
            .GetMethod("AwaitTriggerToActivateEndpointAction", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(server, [endpoint])!;

        await task;
    }

    private static async Task InvokeProcessChannel(SocketServer server, Socket channel, IPEndPoint endpoint)
    {
        var task = (Task)typeof(SocketServer)
            .GetMethod("ProcessChannel", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(server, [channel, endpoint])!;

        await task;
    }

    private static void ReserveProcessingSlot(SocketServer server)
    {
        var semaphore = (SemaphoreSlim)typeof(SocketServer)
            .GetField("_semaphore", BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(server)!;
        semaphore.Wait();
    }

    private static IEnumerable<byte[]> InvokeCollect(SocketServer server, Socket socket, int timeoutMs, int bufferSizeBytes,
        IPEndPoint localEndpoint)
    {
        return (IEnumerable<byte[]>)typeof(SocketServer)
            .GetMethod("Collect", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(server, [socket, timeoutMs, bufferSizeBytes, localEndpoint])!;
    }

    private static SocketServer CreateServer(int port, ProtocolType protocolType, SocketMethod method)
    {
        return new SocketServer(
            CreateConfig(port, protocolType, method),
            Globals.Logger,
            ImmutableList<TransactionStub>.Empty,
            ImmutableList<DataSource>.Empty);
    }

    private static SocketServer CreateServer(ProtocolType protocolType, SocketMethod method)
    {
        return CreateServer(7001, protocolType, method);
    }

    private static SocketServerConfig CreateConfig(int port, ProtocolType protocolType, SocketMethod method)
    {
        return new SocketServerConfig
        {
            Endpoints =
            [
                new SocketEndpointConfig
                {
                    Port = port,
                    ProtocolType = protocolType,
                    SocketType = protocolType == ProtocolType.Udp ? SocketType.Dgram : SocketType.Stream,
                    TimeoutMs = 100,
                    Action = new SocketActionConfig
                    {
                        Name = "ActionA",
                        Method = method
                    }
                }
            ]
        };
    }

    private static int GetFreeTcpPort()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        return ((IPEndPoint)listener.LocalEndpoint).Port;
    }

    private static int GetFreeUdpPort()
    {
        using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
        socket.Bind(new IPEndPoint(IPAddress.Loopback, 0));
        return ((IPEndPoint)socket.LocalEndPoint!).Port;
    }
}
