using System.Net;
using System.Net.Sockets;
using System.Reflection;
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

    [Test]
    public void GetBytesFromChannelWithinTimeout_WithDisposedSocket_ReturnsNull()
    {
        var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        socket.Dispose();

        var bytes = socket.GetBytesFromChannelWithinTimeout(50, 1024, logger: Globals.Logger);

        Assert.That(bytes, Is.Null);
    }

    [Test]
    public void GetDataAsBytesFromChannel_WithUnconnectedTcpSocket_ReturnsEmptyBuffer()
    {
        using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

        var bytes = InvokeGetDataAsBytesFromChannel(socket, 32);

        Assert.That(bytes, Is.Empty);
    }

    [Test]
    public void TryGetRemoteEndPoint_WithDisposedSocket_ReturnsNull()
    {
        var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        socket.Dispose();

        var endpoint = typeof(SocketExtensions)
            .GetMethod("TryGetRemoteEndPoint", BindingFlags.NonPublic | BindingFlags.Static)!
            .Invoke(null, [socket]);

        Assert.That(endpoint, Is.Null);
    }

    private static byte[] InvokeGetDataAsBytesFromChannel(Socket channel, int bufferSize, EndPoint? endpoint = null)
    {
        return (byte[])typeof(SocketExtensions)
            .GetMethod("GetDataAsBytesFromChannel", BindingFlags.NonPublic | BindingFlags.Static)!
            .Invoke(null, [channel, bufferSize, endpoint, Globals.Logger])!;
    }
}
