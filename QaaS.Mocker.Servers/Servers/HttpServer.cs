using System.Collections.Immutable;
using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using QaaS.Mocker.Servers.ConfigurationObjects.HttpServerConfigs;
using QaaS.Mocker.Servers.Extensions;
using QaaS.Mocker.Servers.ServerStates;
using QaaS.Mocker.Stubs.Stubs;

namespace QaaS.Mocker.Servers.Servers;

/// <summary>
/// Represents an HTTP/HTTPS server that handles incoming requests and processes them using transaction stubs.
/// </summary>
public class HttpServer : IServer
{
    private readonly ILogger _logger;
    private readonly HttpServerConfig _configuration;
    private readonly HttpServerState _httpServerState;

    public IServerState State { get; init; }

    public HttpServer(HttpServerConfig httpServerConfig, ILogger logger, IImmutableList<TransactionStub> transactionStubList)
    {
        _logger = logger;
        _configuration = httpServerConfig;
        _httpServerState = new HttpServerState(
            logger,
            transactionStubList,
            httpServerConfig.NotFoundTransactionStubName,
            httpServerConfig.InternalErrorTransactionStubName,
            httpServerConfig.Endpoints);
        State = _httpServerState;
    }

    /// <summary>
    /// Starts the HTTP server and blocks the current thread.
    /// </summary>
    public void Start()
    {
        var host = BuildHost();
        _logger.LogInformation("HTTP Server started. Listening on {Schema}://{Host}:{Port}",
            _configuration.IsSecuredSchema ? "https" : "http",
            _configuration.IsLocalhost ? "localhost" : "0.0.0.0",
            _configuration.Port);
        host.Run();
    }

    private IHost BuildHost()
    {
        var builder = WebApplication.CreateBuilder();

        builder.WebHost.ConfigureKestrel(options =>
        {
            var ipAddress = _configuration.IsLocalhost ? IPAddress.Loopback : IPAddress.Any;
            options.Listen(ipAddress, _configuration.Port, listenOptions =>
            {
                if (!_configuration.IsSecuredSchema)
                    return;

                if (string.IsNullOrWhiteSpace(_configuration.CertificatePath))
                    throw new InvalidOperationException(
                        "Http CertificatePath is required when Server.Http.IsSecuredSchema is true.");

                listenOptions.UseHttps(ResolvePath(_configuration.CertificatePath), _configuration.CertificatePassword);
            });

            // Keep parity with previous behavior: allow many concurrent accept loops.
            options.Limits.MaxConcurrentConnections = Environment.ProcessorCount *
                                                      _configuration.ConnectionAcceptanceValue;
        });

        builder.Services.AddRouting();

        var app = builder.Build();
        app.Map("/{**path}", HandleTransactionAsync);
        return app;
    }

    private async Task HandleTransactionAsync(HttpContext context)
    {
        try
        {
            var path = context.Request.Path.Value ?? "/";
            var method = context.Request.Method.ToHttpMethodEnum();
            var requestData = await context.Request.ConstructRequestDataAsync();
            var responseData = _httpServerState.Process(path, method, requestData);
            await context.Response.HandleResponseDataAndCloseAsync(responseData, method);
        }
        catch (Exception exception)
        {
            _logger.LogCritical(exception, "Encountered critical HTTP server error, closing the server.");
            context.Response.StatusCode = StatusCodes.Status500InternalServerError;
            await context.Response.CompleteAsync();
        }
    }

    private static string ResolvePath(string path)
    {
        return Path.IsPathRooted(path)
            ? path
            : Path.Combine(Environment.CurrentDirectory, path);
    }
}
