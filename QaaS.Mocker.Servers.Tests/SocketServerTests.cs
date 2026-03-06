using System.Collections.Immutable;
using System.IO;
using System.Net;
using System.Net.Sockets;
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

    private static IPEndPoint CreateEndpoint() => new(IPAddress.Parse("0.0.0.0"), 7001);

    private static SocketServer CreateServer(ProtocolType protocolType, SocketMethod method)
    {
        return new SocketServer(
            new SocketServerConfig
            {
                Endpoints =
                [
                    new SocketEndpointConfig
                    {
                        Port = 7001,
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
            },
            Globals.Logger,
            ImmutableList<TransactionStub>.Empty,
            ImmutableList<DataSource>.Empty);
    }
}
