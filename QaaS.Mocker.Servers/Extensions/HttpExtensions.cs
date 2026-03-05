using Microsoft.AspNetCore.Http.Extensions;
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
            _ => throw new ArgumentException($"Http Method type '{stringHttpMethod}' is not supported.", nameof(stringHttpMethod))
        };
    }

    /// <summary>
    /// Constructs request data from an <see cref="Microsoft.AspNetCore.Http.HttpRequest"/>.
    /// </summary>
    public static async Task<Data<object>> ConstructRequestDataAsync(this Microsoft.AspNetCore.Http.HttpRequest request)
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
                    Uri = request.GetEncodedUrl() == null ? null : new Uri(request.GetEncodedUrl()),
                    RequestHeaders = request.Headers.ToDictionary(
                        keyValuePair => keyValuePair.Key,
                        keyValuePair => keyValuePair.Value.ToString())
                }
            }
        };
    }

    /// <summary>
    /// Handles response data by setting headers and writing the response body.
    /// </summary>
    public static async Task HandleResponseDataAndCloseAsync(
        this Microsoft.AspNetCore.Http.HttpResponse response,
        Data<object> responseData,
        HttpMethod method)
    {
        var responseDataBody = responseData.Body as byte[] ?? [];

        response.StatusCode = responseData.MetaData?.Http?.StatusCode ?? DefaultStatusCode;

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

        if (method != HttpMethod.Head)
            await response.Body.WriteAsync(responseDataBody);

        await response.CompleteAsync();
    }
}
