using System.Net;
using System.Net.Sockets;
using Microsoft.Extensions.Logging;

namespace QaaS.Mocker.Servers.Extensions;

public static class SocketExtensions
{
    private static byte[] GetDataAsBytesFromChannel(this Socket channel, int bufferSize, EndPoint? endpoint = null,
        ILogger? logger = null)
    {
        var buffer = new byte[bufferSize];
        try
        {
            var receivedBytes = endpoint != null
                ? channel.ReceiveFrom(buffer, ref endpoint)
                : channel.Receive(buffer);

            return receivedBytes <= 0 ? [] : buffer[..receivedBytes];
        }
        catch (SocketException socketException)
        {
            logger?.LogDebug(
                "Received error while trying to read data from client on endpoint - {ChannelEndPoint} - {SocketException}",
                endpoint ?? channel.RemoteEndPoint, socketException);
            return [];
        }
    }

    /// <summary>
    /// Implementation of timeout mechanism on socket stream collecting.
    /// </summary>
    public static byte[]? GetBytesFromChannelWithinTimeout(this Socket channel, int timeout,
        int bufferSize, EndPoint? endpoint = null, ILogger? logger = null)
    {
        using var cts = new CancellationTokenSource(timeout);
        while (!cts.IsCancellationRequested)
        {
            // Getting the message buffer from Socket communication.
            if (channel is { Available: 0, IsBound: true } && endpoint == null) continue;
            var message = channel.GetDataAsBytesFromChannel(bufferSize, endpoint, logger);
            if (message.Length <= 0) continue;
            logger?.LogDebug("Received {NumberOfReceivedBytes} bytes from socket", message.Length);
            return message;
        }

        return null;
    }
}
