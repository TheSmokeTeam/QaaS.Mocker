using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.AspNetCore.Http.Features;
using QaaS.Framework.SDK.Session.DataObjects;
using QaaS.Framework.SDK.Session.MetaDataObjects;
using QaaS.Mocker.Servers.ConfigurationObjects.HttpServerConfigs;
using HttpMethod = QaaS.Mocker.Servers.ConfigurationObjects.HttpServerConfigs.HttpMethod;

namespace QaaS.Mocker.Servers.Extensions;

/// <summary>
/// Provides extension methods for HTTP-related operations.
/// </summary>
public static class HttpExtensions
{
    private const int DefaultStatusCode = 200;

    /// <summary>
    /// Converts a string representation of an HTTP method to the corresponding <see cref="HttpMethod"/> enum.
    /// </summary>
    public static HttpMethod ToHttpMethodEnum(this string stringHttpMethod)
    {
        return stringHttpMethod.ToUpperInvariant() switch
        {
            "GET" => HttpMethod.Get,
            "POST" => HttpMethod.Post,
            "PUT" => HttpMethod.Put,
            "DELETE" => HttpMethod.Delete,
            "HEAD" => HttpMethod.Head,
            "PATCH" => HttpMethod.Patch,
            "OPTIONS" => HttpMethod.Options,
            "TRACE" => HttpMethod.Trace,
            "CONNECT" => HttpMethod.Connect,
            _ => throw new ArgumentException(
                $"Http Method type '{stringHttpMethod}' is not supported.",
                nameof(stringHttpMethod)
            ),
        };
    }

    /// <summary>
    /// Constructs request data from an <see cref="Microsoft.AspNetCore.Http.HttpRequest"/>.
    /// </summary>
    public static async Task<Data<object>> ConstructRequestDataAsync(
        this Microsoft.AspNetCore.Http.HttpRequest request
    )
    {
        await using var memoryStream = new MemoryStream();
        await request.Body.CopyToAsync(memoryStream);

        return new Data<object>
        {
            Body = memoryStream.ToArray(),
            MetaData = new MetaData
            {
                Http = new Http
                {
                    Uri = new Uri(request.GetEncodedUrl()),
                    Version = request.Protocol.StartsWith(
                        "HTTP/",
                        StringComparison.OrdinalIgnoreCase
                    )
                        ? request.Protocol[5..]
                        : request.Protocol,
                    RequestHeaders = request.Headers.ToDictionary(
                        keyValuePair => keyValuePair.Key,
                        keyValuePair => keyValuePair.Value.ToString()
                    ),
                },
            },
        };
    }

    /// <summary>
    /// Handles response data by setting headers and writing the response body.
    /// </summary>
    public static async Task HandleResponseDataAndCloseAsync(
        this Microsoft.AspNetCore.Http.HttpResponse response,
        Data<object> responseData,
        HttpMethod method
    )
    {
        var responseDataBody = responseData.Body as byte[] ?? [];

        var metadata = responseData.MetaData?.Http;
        response.StatusCode = metadata?.StatusCode ?? DefaultStatusCode;
        var bodyAllowed =
            method != HttpMethod.Head
            && response.StatusCode >= 200
            && response.StatusCode is not (204 or 205 or 304);
        var trailers = metadata?.TrailingHeaders;
        if (trailers is { Count: > 0 } && (!bodyAllowed || !response.SupportsTrailers()))
            throw new NotSupportedException(
                "HTTP response trailers require a body-capable response and server trailer support."
            );
        if (metadata?.ReasonPhrase is { } reason)
        {
            var feature =
                response.HttpContext.Features.Get<IHttpResponseFeature>()
                ?? throw new NotSupportedException(
                    "The server does not expose HTTP response features."
                );
            feature.ReasonPhrase = reason;
        }

        if (responseData.MetaData?.Http?.ResponseHeaders != null)
        {
            foreach (var header in responseData.MetaData.Http.ResponseHeaders)
                response.Headers[header.Key] = header.Value;
        }

        if (responseData.MetaData?.Http?.Headers != null)
        {
            foreach (var header in responseData.MetaData.Http.Headers)
                response.Headers[header.Key] = header.Value;
        }

        // A payload supplied by a processor must not cause Kestrel to reject a bodyless response.
        if (response.StatusCode < 200 || response.StatusCode == 204)
        {
            response.Headers.Remove("Content-Length");
            response.Headers.Remove("Transfer-Encoding");
        }
        else if (response.StatusCode == 205)
        {
            response.Headers.Remove("Transfer-Encoding");
            response.ContentLength = 0;
        }
        if (trailers is { Count: > 0 })
        {
            response.ContentLength = null;
            foreach (var trailer in trailers)
                response.DeclareTrailer(trailer.Key);
        }
        if (bodyAllowed)
            await response.Body.WriteAsync(responseDataBody).ConfigureAwait(false);
        if (trailers is { Count: > 0 })
            foreach (var trailer in trailers)
                response.AppendTrailer(trailer.Key, trailer.Value);

        await response.CompleteAsync().ConfigureAwait(false);
    }
}
