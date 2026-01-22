using System.Collections.Immutable;
using System.Net;
using Microsoft.Extensions.Logging;
using QaaS.Mocker.Servers.ConfigurationObjects.HttpServerConfigs;
using QaaS.Mocker.Servers.Extensions;
using QaaS.Mocker.Servers.ServerStates;
using QaaS.Mocker.Stubs.Stubs;

namespace QaaS.Mocker.Servers.Servers;

/// <summary>
/// Represents an HTTP server that handles incoming requests and processes them using transaction stubs.
/// </summary>
public class HttpServer : IServer
{
    public IServerState State { get; init; }
    
    private const string HttpSchema = "http";
    private const string HttpsSchema = "https";
    private const string LocalhostEndpoint = "localhost";
    private const string OpenEndpoint = "*";
    
    private ILogger _logger;
    private HttpListener _httpListener;
    private HttpServerState _httpServerState;
    private Semaphore _semaphore;

    /// <summary>
    /// Initializes a new instance of the <see cref="HttpServer"/> class.
    /// </summary>
    /// <param name="httpServerConfig">Configuration for the HTTP server.</param>
    /// <param name="logger">Logger for logging server activities.</param>
    /// <param name="transactionStubList">List of transaction stubs to handle requests.</param>
    public HttpServer(HttpServerConfig httpServerConfig, ILogger logger, IImmutableList<TransactionStub> transactionStubList)
    {
        _logger = logger;
        _httpListener = new HttpListener();
        _httpServerState = new HttpServerState(
            logger, 
            transactionStubList, 
            httpServerConfig.NotFoundTransactionStubName, 
            httpServerConfig.InternalErrorTransactionStubName, 
            httpServerConfig.Endpoints
        );
        State = _httpServerState;
        InitializeSemaphore(httpServerConfig.ConnectionAcceptanceValue);
        InitiatePrefix(httpServerConfig.Port, httpServerConfig.IsSecuredSchema, httpServerConfig.IsLocalhost);
    }
    
    /// <summary>
    /// Initializes the semaphore used to control the number of concurrent connections.
    /// </summary>
    /// <param name="connectionAcceptanceValue">Base value for connection acceptance.</param>
    private void InitializeSemaphore(int connectionAcceptanceValue)
    {
        var maxConnections = Environment.ProcessorCount * connectionAcceptanceValue;
        _semaphore = new Semaphore(maxConnections, maxConnections);
        _logger.LogDebug($"Connection Acceptance Semaphore initialized with max connections of {maxConnections}");
    }
    
    /// <summary>
    /// Configures the HTTP listener prefix based on the server configuration.
    /// </summary>
    /// <param name="port">Port number for the server.</param>
    /// <param name="isSecuredSchema">Indicates if the server uses HTTPS.</param>
    /// <param name="isLocalhost">Indicates if the server listens on localhost.</param>
    private void InitiatePrefix(int port, bool isSecuredSchema = false, bool isLocalhost = false)
    {
        var schemaPrefix = isSecuredSchema ? HttpsSchema : HttpSchema;
        var host = isLocalhost ? LocalhostEndpoint : OpenEndpoint;
        var prefix = $"{schemaPrefix}://{host}:{port}/";
        _httpListener.Prefixes.Add(prefix);
        _logger.LogInformation("Configured HTTP Server prefix to '{Prefix}'", prefix);
    }


    /// <summary>
    /// Starts the HTTP server and begins listening for incoming requests.
    /// </summary>
    public void Start()
    {
        _httpListener.Start();
        _logger.LogInformation("HTTP Server started. Listening for incoming connections...");
        
        while (true)
        {
            _semaphore.WaitOne();
            _httpListener.GetContextAsync().ContinueWith(ProcessContext);
        }
    }

    /// <summary>
    /// Processes the HTTP context asynchronously.
    /// </summary>
    /// <param name="task">Task representing the HTTP context.</param>
    private async Task ProcessContext(Task<HttpListenerContext> task)
    {
        try
        {
            _semaphore.Release();
            var context = await task;
            await HandleTransaction(context.Request, context.Response);
        }
        catch (Exception exception)
        {
            _logger.LogCritical(exception, "Encountered critical HTTP server error, closing the server.");
            Environment.Exit(1);
        }
    }

    /// <summary>
    /// Handles an incoming HTTP request and sends the appropriate response.
    /// </summary>
    /// <param name="request">HTTP request.</param>
    /// <param name="response">HTTP response.</param>
    private async Task HandleTransaction(HttpListenerRequest request, HttpListenerResponse response)
    {
        var path = request.Url!.AbsolutePath;
        var method = request.HttpMethod.ToHttpMethodEnum();
        var requestData = request.ConstructRequestData();
        var responseData = _httpServerState.Process(path, method, requestData);
        response.HandleResponseDataAndClose(responseData, method);
    }
}