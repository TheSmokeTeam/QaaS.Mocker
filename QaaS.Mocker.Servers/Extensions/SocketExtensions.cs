using System.Net;
using System.Net.Sockets;
using Microsoft.Extensions.Logging;

namespace QaaS.Mocker.Servers.Extensions;

public static class SocketExtensions
{
    private static Span<byte> GetDataAsSpanOfBytesFromChannel(this Socket channel, long bufferSize, EndPoint? endpoint = null,
        ILogger? logger = null)
    {
        var buffer = new byte[bufferSize].AsSpan();
        try
        {
            if (endpoint != null)
                channel.ReceiveFrom(buffer, ref endpoint);
            else
                channel.Receive(buffer);
        }
        catch (SocketException socketException)
        {
            logger?.LogDebug(
                "Received error while trying to read data from client on endpoint - {ChannelEndPoint} - {SocketException}",
                endpoint ?? channel.RemoteEndPoint, socketException);
            return Span<byte>.Empty;
        }

        return buffer;
    }

    /// <summary>
    /// Implementation of timeout mechanism on socket stream collecting.
    /// </summary>
    public static byte[]? GetBytesFromChannelWithinTimeout(this Socket channel, int timeout,
        long bufferSize, EndPoint? endpoint = null, ILogger? logger = null)
    {
        using var cts = new CancellationTokenSource(timeout);
        while (!cts.IsCancellationRequested)
        {
            // Getting the message buffer from Socket communication.
            if (channel is { Available: 0, IsBound: true }) continue;
            var message = channel.GetDataAsSpanOfBytesFromChannel(bufferSize, endpoint, logger);
            if (message.Length <= 0) continue;
            logger?.LogDebug("Received {NumberOfReceivedBytes} bytes from socket", message.Length);
            return message.ToArray();
        }

        return null;
    }
}