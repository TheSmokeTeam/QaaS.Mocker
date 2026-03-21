using System.Net;
using System.Net.Sockets;
using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace QaaS.Mocker.Servers.Extensions;

/// <summary>
/// Socket transport helpers for bounded reads and diagnostic endpoint formatting.
/// </summary>
public static class SocketExtensions
{
    /// <summary>
    /// Reads a single payload from a socket and returns exactly the bytes reported by the transport.
    /// This avoids padding payloads with unused buffer capacity, which previously broke protocol
    /// framing and request deserialization.
    /// </summary>
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
            logger?.LogDebug(socketException,
                "Socket receive failed on local endpoint '{LocalEndPoint}' and remote endpoint '{RemoteEndPoint}'",
                DescribeEndPoint(channel.LocalEndPoint), DescribeEndPoint(endpoint ?? TryGetRemoteEndPoint(channel)));
            return [];
        }
    }

    /// <summary>
    /// Implements timeout-based socket collection.
    /// When <paramref name="endpoint"/> is provided the call is treated as datagram-based receive,
    /// so the loop does not rely on <see cref="Socket.Available"/> before reading.
    /// </summary>
    public static byte[]? GetBytesFromChannelWithinTimeout(this Socket channel, int timeout,
        int bufferSize, EndPoint? endpoint = null, ILogger? logger = null)
    {
        var timeoutStopwatch = Stopwatch.StartNew();
        while (timeoutStopwatch.ElapsedMilliseconds < timeout)
        {
            var remainingTimeoutMs = Math.Max(1, timeout - (int)timeoutStopwatch.ElapsedMilliseconds);
            var pollTimeoutMicroseconds = Math.Min(remainingTimeoutMs, 50) * 1000;
            try
            {
                if (!channel.Poll(pollTimeoutMicroseconds, SelectMode.SelectRead))
                    continue;
            }
            catch (SocketException socketException)
            {
                logger?.LogDebug(socketException,
                    "Socket poll failed on local endpoint '{LocalEndPoint}' and remote endpoint '{RemoteEndPoint}'",
                    DescribeEndPoint(channel.LocalEndPoint), DescribeEndPoint(endpoint ?? TryGetRemoteEndPoint(channel)));
                return null;
            }
            catch (ObjectDisposedException)
            {
                return null;
            }

            var message = channel.GetDataAsBytesFromChannel(bufferSize, endpoint, logger);
            if (message.Length <= 0)
                continue;
            logger?.LogDebug(
                "Received {NumberOfReceivedBytes} bytes on local endpoint '{LocalEndPoint}' from remote endpoint '{RemoteEndPoint}'",
                message.Length,
                DescribeEndPoint(channel.LocalEndPoint),
                DescribeEndPoint(endpoint ?? TryGetRemoteEndPoint(channel)));
            return message;
        }

        logger?.LogDebug(
            "Timed out waiting {TimeoutMs} ms for socket data on local endpoint '{LocalEndPoint}'",
            timeout, DescribeEndPoint(channel.LocalEndPoint));
        return null;
    }

    private static EndPoint? TryGetRemoteEndPoint(Socket channel)
    {
        try
        {
            return channel.RemoteEndPoint;
        }
        catch (SocketException)
        {
            return null;
        }
        catch (ObjectDisposedException)
        {
            return null;
        }
    }

    private static string DescribeEndPoint(EndPoint? endpoint) => endpoint?.ToString() ?? "<unresolved>";
}
