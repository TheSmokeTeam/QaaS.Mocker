using System.Net;
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
    private const int BufferOffset = 0;
    private const int DefaultStatusCode = 200;
    
    /// <summary>
    /// Converts a string representation of an HTTP method to the corresponding <see cref="HttpMethod"/> enum.
    /// </summary>
    /// <param name="stringHttpMethod">The string representation of the HTTP method.</param>
    /// <returns>The corresponding <see cref="HttpMethod"/> enum.</returns>
    /// <exception cref="ArgumentException">Thrown when the HTTP method is not supported.</exception>
    public static HttpMethod ToHttpMethodEnum(this string stringHttpMethod)
    {
        return stringHttpMethod switch
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
            _ => throw new ArgumentException("Http Method type not supported!", stringHttpMethod)
        };
    }
    
    /// <summary>
    /// Converts an <see cref="HttpContentType"/> enum to its corresponding string representation.
    /// </summary>
    /// <param name="contentType">The <see cref="HttpContentType"/> enum.</param>
    /// <returns>The string representation of the HTTP content type.</returns>
    /// <exception cref="ArgumentException">Thrown when the HTTP content type is not supported.</exception>
    public static string ToHeaderString(this HttpContentType contentType)
    {
        return contentType switch
        {
            HttpContentType.TextPlain => "text/plain",
            HttpContentType.ApplicationJson => "application/json",
            _ => throw new ArgumentException("Http Content type not supported!", contentType.ToString())
        };
    }
    
    /// <summary>
    /// Constructs request data from an <see cref="HttpListenerRequest"/>.
    /// Appends current Datetime UTC to Timestamp. 
    /// </summary>
    /// <param name="request">The <see cref="HttpListenerRequest"/> to construct data from.</param>
    /// <returns>A data containing the request data.</returns>
    public static Data<object> ConstructRequestData(this HttpListenerRequest request)
    {
        byte[] bodyBytes;
        using (var memoryStream = new MemoryStream())
        {
            using (var requestStream = request.InputStream)
            {
                requestStream.CopyTo(memoryStream);
            }
            bodyBytes = memoryStream.ToArray();
        }
        return new Data<object>
        {
            Body = bodyBytes,
            MetaData = new MetaData
            {
                Http = new Http
                {
                    Uri = request.Url,
                    RequestHeaders = request.Headers.AllKeys.Select(headerKey => 
                        new KeyValuePair<string, string>(headerKey!, request.Headers[headerKey]!)).ToDictionary()
                }
            }
        };
    }

    /// <summary>
    /// Handles response data by setting the appropriate headers and writing the response body to
    /// the <see cref="HttpListenerResponse"/>, then closing it.
    /// "Head" Http Method doesn't return any body.
    /// </summary>
    /// <param name="response">The <see cref="HttpListenerResponse"/> to handle.</param>
    /// <param name="responseData">The response data to handle.</param>
    /// <param name="method">The method of the transaction.</param>
    public static void HandleResponseDataAndClose(this HttpListenerResponse response, Data<object> responseData, 
        HttpMethod method)
    {
        byte[] responseDataBody;
        if (responseData.Body == null) responseDataBody = Array.Empty<byte>();
        else responseDataBody = (responseData.Body as byte[])!;
        
        response.StatusCode = responseData.MetaData?.Http?.StatusCode.HasValue == true ? 
            responseData.MetaData.Http.StatusCode.Value : DefaultStatusCode;

        if (responseData.MetaData?.Http?.ResponseHeaders != null)
            foreach (var header in responseData.MetaData.Http.ResponseHeaders)
                response.Headers[header.Key] = header.Value;
        
        if (responseData.MetaData?.Http?.Headers != null)
            foreach (var header in responseData.MetaData.Http.Headers)
                response.Headers[header.Key] = header.Value;

        if (method != HttpMethod.Head)
        {
            response.ContentLength64 = responseDataBody.Length;
            response.OutputStream.Write(responseDataBody, BufferOffset, responseDataBody.Length);
        }
        
        response.OutputStream.Flush();
        response.Close();
    }
}