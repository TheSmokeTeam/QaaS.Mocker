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
using QaaS.Mocker.Servers.Metrics;
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
    private readonly int? _httpMetricsSlot;

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
        _httpMetricsSlot = httpServerConfig.EnableHttpMetrics
            ? HttpRequestMetrics.ValidateAvailableSlot(httpServerConfig.HttpMetricsSlot)
            : null;
    }

    /// <summary>
    /// Starts the HTTP server and blocks the current thread.
    /// </summary>
    public void Start()
    {
        if (_httpMetricsSlot is not { } slot)
        {
            // Keep the default host lifecycle and logging order unchanged.
            var host = BuildHost(null);
            LogListening();
            host.Run();
            return;
        }

        var metrics = HttpRequestMetrics.Reserve(slot);
        try
        {
            using var host = BuildHost(metrics);
            host.Start();
            metrics.Activate();
            LogListening();
            host.WaitForShutdown();
        }
        finally
        {
            // Dispose the failed host first. Successful startup (or an already accepted real request)
            // permanently claims the slot, even if later logging or shutdown fails.
            metrics.ReleaseUnactivated();
        }
    }

    private void LogListening() =>
        _logger.LogInformation("HTTP Server started. Listening on {Schema}://{Host}:{Port}",
            _configuration.IsSecuredSchema ? "https" : "http",
            _configuration.IsLocalhost ? "localhost" : "0.0.0.0",
            _configuration.Port);

    private IHost BuildHost(HttpRequestMetrics? metrics)
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

                var certificatePath = ResolvePath(_configuration.CertificatePath);
                if (!File.Exists(certificatePath))
                    throw new InvalidOperationException(
                        $"Configured HTTPS certificate '{certificatePath}' was not found. Current working directory: '{Environment.CurrentDirectory}'.");

                _logger.LogInformation("Configuring HTTPS certificate from '{CertificatePath}'", certificatePath);
                listenOptions.UseHttps(certificatePath, _configuration.CertificatePassword);
            });

            // Keep parity with previous behavior: allow many concurrent accept loops.
            options.Limits.MaxConcurrentConnections = Environment.ProcessorCount *
                                                      _configuration.ConnectionAcceptanceValue;
        });

        builder.Services.AddRouting();

        var app = builder.Build();
        app.Map("/{**path}", context => HandleTransactionAsync(context, metrics));
        return app;
    }

    private async Task HandleTransactionAsync(HttpContext context, HttpRequestMetrics? metrics)
    {
        var started = metrics?.Start();
        var outcome = HttpRequestOutcome.Error;
        try
        {
            var path = context.Request.Path.Value ?? "/";
            var method = context.Request.Method.ToHttpMethodEnum();
            var requestData = await context.Request.ConstructRequestDataAsync();
            var responseData = _httpServerState.Process(path, method, requestData, out var dispatchOutcome);
            await context.Response.HandleResponseDataAndCloseAsync(responseData, method);
            outcome = dispatchOutcome;
        }
        catch (Exception exception)
        {
            _logger.LogCritical(exception, "Encountered critical HTTP server error, closing the server.");
            context.Response.StatusCode = StatusCodes.Status500InternalServerError;
            await context.Response.CompleteAsync();
        }
        finally
        {
            if (started is { } timestamp)
                metrics!.Complete(outcome, timestamp);
        }
    }

    private static string ResolvePath(string path)
    {
        return Path.IsPathRooted(path)
            ? path
            : Path.Combine(Environment.CurrentDirectory, path);
    }
}
