using System.Net;
using System.Net.Sockets;
using System.Text;
using NUnit.Framework;
using QaaS.Mocker.Servers.Extensions;

namespace QaaS.Mocker.Servers.Tests.ExtensionsTests;

[TestFixture]
public class SocketExtensionsTests
{
    [Test]
    public async Task GetBytesFromChannelWithinTimeout_WithTcpPayload_ReturnsOnlyReceivedBytes()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();

        var client = new TcpClient();
        var connectTask = client.ConnectAsync(IPAddress.Loopback, ((IPEndPoint)listener.LocalEndpoint).Port);
        using var serverSocket = await listener.AcceptSocketAsync();
        await connectTask;

        await using var clientStream = client.GetStream();
        var payload = Encoding.UTF8.GetBytes("hello");
        await clientStream.WriteAsync(payload);
        await clientStream.FlushAsync();

        var bytes = serverSocket.GetBytesFromChannelWithinTimeout(500, 1024, logger: Globals.Logger);

        Assert.That(Encoding.UTF8.GetString(bytes!), Is.EqualTo("hello"));
    }

    [Test]
    public void GetBytesFromChannelWithinTimeout_WithUdpPayload_ReturnsOnlyReceivedBytes()
    {
        using var receiver = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
        receiver.Bind(new IPEndPoint(IPAddress.Loopback, 0));

        using var sender = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
        var payload = Encoding.UTF8.GetBytes("udp");
        sender.SendTo(payload, receiver.LocalEndPoint!);

        EndPoint remoteEndpoint = new IPEndPoint(IPAddress.Any, 0);
        var bytes = receiver.GetBytesFromChannelWithinTimeout(500, 1024, remoteEndpoint, Globals.Logger);

        Assert.That(Encoding.UTF8.GetString(bytes!), Is.EqualTo("udp"));
    }

    [Test]
    public async Task GetBytesFromChannelWithinTimeout_WithNoTcpPayload_ReturnsNullAfterTimeout()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();

        using var client = new TcpClient();
        var connectTask = client.ConnectAsync(IPAddress.Loopback, ((IPEndPoint)listener.LocalEndpoint).Port);
        using var serverSocket = await listener.AcceptSocketAsync();
        await connectTask;

        var bytes = serverSocket.GetBytesFromChannelWithinTimeout(50, 1024, logger: Globals.Logger);

        Assert.That(bytes, Is.Null);
    }
}
