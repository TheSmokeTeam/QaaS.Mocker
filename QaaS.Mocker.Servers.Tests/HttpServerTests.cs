using System.Collections.Immutable;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics.Metrics;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NUnit.Framework;
using QaaS.Framework.SDK.ContextObjects;
using QaaS.Framework.SDK.DataSourceObjects;
using QaaS.Framework.SDK.Hooks.Processor;
using QaaS.Framework.SDK.Session.DataObjects;
using QaaS.Framework.SDK.Session.MetaDataObjects;
using QaaS.Mocker.Servers.ConfigurationObjects.HttpServerConfigs;
using QaaS.Mocker.Servers.Metrics;
using QaaS.Mocker.Servers.Servers;
using QaaS.Mocker.Stubs;
using QaaS.Mocker.Stubs.Stubs;
using HttpMethod = QaaS.Mocker.Servers.ConfigurationObjects.HttpServerConfigs.HttpMethod;

namespace QaaS.Mocker.Servers.Tests;

[TestFixture]
public class HttpServerTests
{
    [Test]
    public async Task HandleTransactionAsync_WithMatchingEndpoint_WritesStubResponse()
    {
        var server = CreateServer();
        var context = new DefaultHttpContext();
        context.Request.Method = "GET";
        context.Request.Scheme = "http";
        context.Request.Host = new HostString("localhost", 8080);
        context.Request.Path = "/health";
        context.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes("request"));
        context.Response.Body = new MemoryStream();

        await InvokeHandleTransactionAsync(server, context);

        context.Response.Body.Position = 0;
        using var reader = new StreamReader(context.Response.Body, Encoding.UTF8, leaveOpen: true);
        var payload = await reader.ReadToEndAsync();

        Assert.Multiple(() =>
        {
            Assert.That(context.Response.StatusCode, Is.EqualTo(StatusCodes.Status200OK));
            Assert.That(payload, Is.EqualTo("ok"));
        });
    }

    [Test]
    public async Task HandleTransactionAsync_WithInvalidHttpMethod_ReturnsInternalServerError()
    {
        var server = CreateServer();
        var context = new DefaultHttpContext();
        context.Request.Method = "BREW";
        context.Request.Path = "/health";
        context.Request.Body = new MemoryStream();
        context.Response.Body = new MemoryStream();

        await InvokeHandleTransactionAsync(server, context);

        Assert.That(context.Response.StatusCode, Is.EqualTo(StatusCodes.Status500InternalServerError));
    }

    [Test]
    public void BuildHost_WithHttpConfiguration_ReturnsHostInstance()
    {
        var server = CreateServer();

        using var host = InvokeBuildHost(server);

        Assert.That(host, Is.Not.Null);
    }

    [Test]
    public void BuildHost_WithNonLocalhostConfiguration_ReturnsHostInstance()
    {
        var server = CreateServer(new HttpServerConfig
        {
            Port = 0,
            IsLocalhost = false,
            Endpoints = CreateEndpoints()
        });

        using var host = InvokeBuildHost(server);

        Assert.That(host, Is.Not.Null);
    }

    [Test]
    public void BuildHost_WithSecuredSchemaAndMissingCertificatePath_ThrowsInvalidOperationException()
    {
        var server = CreateServer(new HttpServerConfig
        {
            Port = 0,
            IsLocalhost = true,
            IsSecuredSchema = true,
            Endpoints = CreateEndpoints()
        });

        var exception = Assert.Throws<TargetInvocationException>(() => InvokeBuildHost(server));

        Assert.That(exception!.InnerException, Is.TypeOf<InvalidOperationException>());
    }

    [Test]
    public void BuildHost_WithMissingCertificateFile_ThrowsInvalidOperationException()
    {
        var server = CreateServer(new HttpServerConfig
        {
            Port = 0,
            IsLocalhost = true,
            IsSecuredSchema = true,
            CertificatePath = "missing-certificate.pfx",
            Endpoints = CreateEndpoints()
        });

        var exception = Assert.Throws<TargetInvocationException>(() => InvokeBuildHost(server));

        Assert.That(exception!.InnerException, Is.TypeOf<InvalidOperationException>());
    }

    [Test]
    public void BuildHost_WithValidHttpsCertificate_ReturnsHostInstance()
    {
        var certificatePath = CreateCertificateFile();
        try
        {
            var server = CreateServer(new HttpServerConfig
            {
                Port = 0,
                IsLocalhost = true,
                IsSecuredSchema = true,
                CertificatePath = certificatePath,
                CertificatePassword = "password",
                Endpoints = CreateEndpoints()
            });

            using var host = InvokeBuildHost(server);

            Assert.That(host, Is.Not.Null);
        }
        finally
        {
            File.Delete(certificatePath);
        }
    }

    [Test]
    public void ResolvePath_WithRelativePath_UsesCurrentDirectory()
    {
        var resolved = (string)typeof(HttpServer)
            .GetMethod("ResolvePath", BindingFlags.NonPublic | BindingFlags.Static)!
            .Invoke(null, ["certs/server.pfx"])!;

        Assert.That(resolved, Is.EqualTo(Path.Combine(Environment.CurrentDirectory, "certs/server.pfx")));
    }

    [Test]
    public void ResolvePath_WithAbsolutePath_ReturnsPathUnchanged()
    {
        var absolutePath = Path.Combine(Path.GetTempPath(), "server.pfx");
        var resolved = (string)typeof(HttpServer)
            .GetMethod("ResolvePath", BindingFlags.NonPublic | BindingFlags.Static)!
            .Invoke(null, [absolutePath])!;

        Assert.That(resolved, Is.EqualTo(absolutePath));
    }

    [Test]
    public void Start_WithHttpConfiguration_StartsUntilInterrupted()
    {
        var server = CreateServer(new HttpServerConfig
        {
            Port = 0,
            IsLocalhost = true,
            IsSecuredSchema = false,
            Endpoints = CreateEndpoints()
        });

        RunServerStartUntilInterrupted(server);
    }

    [Test]
    public void Start_WithHttpsAndNonLocalhost_StartsUntilInterrupted()
    {
        var certificatePath = CreateCertificateFile();
        try
        {
            var server = CreateServer(new HttpServerConfig
            {
                Port = 0,
                IsLocalhost = false,
                IsSecuredSchema = true,
                CertificatePath = certificatePath,
                CertificatePassword = "password",
                Endpoints = CreateEndpoints()
            });

            RunServerStartUntilInterrupted(server);
        }
        finally
        {
            File.Delete(certificatePath);
        }
    }

    [Test]
    public async Task HandleTransactionAsync_WithRootPath_WritesStubResponse()
    {
        var server = CreateServer(new HttpServerConfig
        {
            Port = 0,
            IsLocalhost = true,
            Endpoints =
            [
                new HttpEndpointConfig
                {
                    Path = "/",
                    Actions =
                    [
                        new HttpEndpointActionConfig
                        {
                            Name = "Root",
                            Method = HttpMethod.Get,
                            TransactionStubName = "MainStub"
                        }
                    ]
                }
            ]
        });
        var context = new DefaultHttpContext();
        context.Request.Method = "GET";
        context.Request.Scheme = "http";
        context.Request.Host = new HostString("localhost", 8080);
        context.Request.Path = "/";
        context.Request.Body = new MemoryStream();
        context.Response.Body = new MemoryStream();

        await InvokeHandleTransactionAsync(server, context);

        context.Response.Body.Position = 0;
        using var reader = new StreamReader(context.Response.Body, Encoding.UTF8, leaveOpen: true);
        var payload = await reader.ReadToEndAsync();

        Assert.That(payload, Is.EqualTo("ok"));
    }

    [TestCase(false)]
    [TestCase(true)]
    [NonParallelizable]
    public async Task HttpMetrics_UseDispatchOutcomeRatherThanStatus_OnlyWhenEnabled(bool enabled)
    {
        const string meterName = "QaaS.Mocker.Http";
        var counters = new List<(string Name, long Value, KeyValuePair<string, object?>[] Tags)>();
        var durations = new List<(string Name, string? Unit, double Value, KeyValuePair<string, object?>[] Tags)>();
        using var listener = new MeterListener
        {
            InstrumentPublished = (instrument, owner) =>
            {
                if (instrument.Meter.Name == meterName)
                    owner.EnableMeasurementEvents(instrument);
            }
        };
        listener.SetMeasurementEventCallback<long>((i, v, tags, _) => counters.Add((i.Name, v, tags.ToArray())));
        listener.SetMeasurementEventCallback<double>((i, v, tags, _) =>
            durations.Add((i.Name, i.Unit, v, tags.ToArray())));
        listener.Start();

        var server = new HttpServer(new HttpServerConfig
        {
            Port = 0,
            IsLocalhost = true,
            EnableHttpMetrics = enabled,
            HttpMetricsSlot = enabled ? 1 : 99,
            Endpoints =
            [
                new HttpEndpointConfig
                {
                    Path = "/matched",
                    Actions = [new HttpEndpointActionConfig { Name = "Match", Method = HttpMethod.Get,
                        TransactionStubName = "Configured404" }]
                },
                new HttpEndpointConfig
                {
                    Path = "/broken",
                    Actions = [new HttpEndpointActionConfig { Name = "Broken", Method = HttpMethod.Get,
                        TransactionStubName = "Throw" }]
                }
            ]
        }, Globals.Logger,
        [
            CreateStub("Configured404", _ => CreateResponse("configured", StatusCodes.Status404NotFound)),
            CreateStub("Throw", _ => throw new InvalidOperationException("synthetic failure")),
            CreateStub(Constants.DefaultNotFoundTransactionStubLabel, _ => CreateResponse("miss")),
            CreateStub(Constants.DefaultInternalErrorTransactionStubLabel, _ => CreateResponse("error",
                StatusCodes.Status202Accepted))
        ]);
        var metrics = enabled ? HttpRequestMetrics.Reserve(1) : null;
        metrics?.Activate();
        listener.RecordObservableInstruments();
        var before = counters.ToDictionary(x => (x.Name, Slot: (int)x.Tags.Single(t => t.Key == "metric_slot").Value!,
            Outcome: x.Tags.FirstOrDefault(t => t.Key == "outcome").Value as string), x => x.Value);
        counters.Clear();

        foreach (var (method, path, expected) in new[]
        {
            ("GET", "/matched", StatusCodes.Status404NotFound),
            ("GET", "/unknown", StatusCodes.Status200OK),
            ("POST", "/matched", StatusCodes.Status200OK),
            ("GET", "/broken", StatusCodes.Status202Accepted),
            ("BREW", "/matched", StatusCodes.Status500InternalServerError)
        })
        {
            var context = new DefaultHttpContext();
            context.Request.Method = method;
            context.Request.Path = path;
            context.Request.Scheme = "http";
            context.Request.Host = new HostString("localhost", 8080);
            await using var body = new MemoryStream();
            context.Response.Body = body;
            await InvokeHandleTransactionAsync(server, context, metrics);
            Assert.That(context.Response.StatusCode, Is.EqualTo(expected), $"{method} {path}");
        }

        listener.RecordObservableInstruments();
        var after = counters.ToDictionary(x => (x.Name, Slot: (int)x.Tags.Single(t => t.Key == "metric_slot").Value!,
            Outcome: x.Tags.FirstOrDefault(t => t.Key == "outcome").Value as string), x => x.Value);
        if (!enabled)
        {
            Assert.That(after, Is.EqualTo(before));
            Assert.That(durations, Is.Empty);
            return;
        }

        var slotCounters = counters.Where(x => x.Tags.Any(tag => tag is { Key: "metric_slot", Value: 1 })).ToArray();
        var accepted = slotCounters.Where(x => x.Name == "qaas.mocker.requests.accepted").ToArray();
        var completed = slotCounters.Where(x => x.Name == "qaas.mocker.requests.completed").ToArray();
        Assert.Multiple(() =>
        {
            Assert.That(slotCounters, Has.Length.EqualTo(4));
            Assert.That(accepted, Has.Length.EqualTo(1));
            Assert.That(accepted.Single().Tags, Has.Length.EqualTo(1));
            Assert.That(completed.All(x => x.Tags.Length == 2 && x.Tags.Any(tag => tag.Key == "outcome")), Is.True);
            Assert.That(completed.Select(x => x.Tags.Single(tag => tag.Key == "outcome").Value),
                Is.EquivalentTo(new[] { "hit", "miss", "error" }));
            Assert.That(after[("qaas.mocker.requests.accepted", 1, null)] - before[("qaas.mocker.requests.accepted", 1, null)],
                Is.EqualTo(5));
            foreach (var (key, increase) in new[] { ("hit", 1L), ("miss", 2L), ("error", 2L) })
                Assert.That(after[("qaas.mocker.requests.completed", 1, key)] -
                    before[("qaas.mocker.requests.completed", 1, key)], Is.EqualTo(increase), key);
            Assert.That(durations, Has.Count.EqualTo(5));
            Assert.That(durations.All(x => x.Name == "qaas.mocker.request.duration" &&
                x.Unit == "s" && double.IsFinite(x.Value) && x.Value >= 0 &&
                x.Tags is [{ Key: "metric_slot", Value: 1 }]), Is.True);
        });
    }

    [Test]
    [NonParallelizable]
    public async Task HttpMetrics_InvalidOrDuplicateOwnerLeavesExistingHandlerIntact()
    {
        static HttpServerConfig Config(int slot) => new()
        {
            IsLocalhost = true,
            EnableHttpMetrics = true,
            HttpMetricsSlot = slot,
            Endpoints = CreateEndpoints()
        };
        var server = CreateServer(Config(2));
        var metrics = HttpRequestMetrics.Reserve(2);
        metrics.Activate();
        Assert.That(() => CreateServer(Config(2)),
            Throws.TypeOf<InvalidOperationException>().With.Message.Contains("already assigned"));
        Assert.That(() => CreateServer(Config(9)), Throws.TypeOf<ArgumentOutOfRangeException>());

        var context = new DefaultHttpContext();
        context.Request.Method = "GET";
        context.Request.Scheme = "http";
        context.Request.Host = new HostString("localhost");
        context.Request.Path = "/health";
        await using var body = new MemoryStream();
        context.Response.Body = body;
        await InvokeHandleTransactionAsync(server, context, metrics);

        Assert.That(context.Response.StatusCode, Is.EqualTo(StatusCodes.Status200OK));
        Assert.That(Encoding.UTF8.GetString(body.ToArray()), Is.EqualTo("ok"));
    }

    [TestCase(false)]
    [TestCase(true)]
    [NonParallelizable]
    public async Task HttpMetrics_FailedBindCanBeRetriedWithANewEmbeddedServer(bool enabled)
    {
        const int slot = 5;
        using var blocker = new TcpListener(IPAddress.Loopback, 0);
        blocker.Start();
        var port = ((IPEndPoint)blocker.LocalEndpoint).Port;
        HttpServerConfig Config() => new()
        {
            Port = port,
            IsLocalhost = true,
            EnableHttpMetrics = enabled,
            HttpMetricsSlot = slot,
            Endpoints = CreateEndpoints()
        };
        var accepted = new List<long>();
        using var listener = new MeterListener
        {
            InstrumentPublished = (instrument, owner) =>
            {
                if (instrument.Meter.Name == "QaaS.Mocker.Http" && instrument.Name == "qaas.mocker.requests.accepted")
                    owner.EnableMeasurementEvents(instrument);
            }
        };
        listener.SetMeasurementEventCallback<long>((_, value, tags, _) =>
        {
            if (tags.Length == 1 && tags[0] is { Key: "metric_slot", Value: slot })
                accepted.Add(value);
        });
        listener.Start();

        var failed = CreateServer(Config());
        listener.RecordObservableInstruments();
        var preBindSeries = accepted.Count;
        accepted.Clear();
        Assert.That(failed.Start, Throws.InstanceOf<IOException>()
            .With.Message.Contains("Failed to bind"));
        listener.RecordObservableInstruments();
        var failedBindSeries = accepted.Count;
        accepted.Clear();
        blocker.Stop();

        // This was the parity failure: disabled reconstruction worked; opted in it threw here.
        var replacement = CreateServer(Config());
        var preconstructedDuplicate = enabled ? CreateServer(Config()) : null;
        Assert.That(preBindSeries, Is.Zero, "A merely constructed owner must not be exported.");
        Assert.That(failedBindSeries, Is.Zero, "Failed pre-bind ownership must not be exported.");
        var thread = StartBackgroundServer(replacement, out var errors);
        try
        {
            await AssertServerResponds(port);
            listener.RecordObservableInstruments();
            Assert.That(accepted, Is.EqualTo(enabled ? new long[] { 1 } : []));
            if (enabled)
            {
                Assert.That(() => CreateServer(Config()), Throws.InvalidOperationException
                    .With.Message.Contains("already assigned"));
                Assert.That(preconstructedDuplicate!.Start, Throws.InvalidOperationException
                    .With.Message.Contains("already assigned"));
            }
        }
        finally
        {
            if (thread.IsAlive)
                thread.Interrupt();
            Assert.That(thread.Join(TimeSpan.FromSeconds(5)), Is.True);
        }
        Assert.That(errors, Is.Empty);
        if (enabled)
        {
            Assert.That(() => CreateServer(Config()), Throws.InvalidOperationException
                .With.Message.Contains("already assigned"), "Never reset a published slot after shutdown.");
            accepted.Clear();
            listener.RecordObservableInstruments();
            Assert.That(accepted, Is.EqualTo(new long[] { 1 }), "Preserve the exported count after shutdown.");
        }
    }

    [Test]
    [NonParallelizable]
    public void HttpMetrics_StartupLogFailureDisposesHostWithoutReleasingAnActivatedSlot()
    {
        // Pick an ephemeral port without using a fixed service port shared with other tests.
        using var portReservation = new TcpListener(IPAddress.Loopback, 0);
        portReservation.Start();
        var port = ((IPEndPoint)portReservation.LocalEndpoint).Port;
        portReservation.Stop();
        var config = new HttpServerConfig
        {
            Port = port,
            IsLocalhost = true,
            EnableHttpMetrics = true,
            HttpMetricsSlot = 6,
            Endpoints = CreateEndpoints()
        };

        var server = CreateServer(config, new ThrowOnListenLogger());
        Assert.That(server.Start, Throws.InvalidOperationException.With.Message.EqualTo("synthetic logging failure"));

        // Do not strand a listener if a logger throws after bind.
        portReservation.Start();
        Assert.That(() => CreateServer(config), Throws.InvalidOperationException.With.Message.Contains("already assigned"));
    }

    private sealed class ThrowOnListenLogger : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            if (formatter(state, exception).StartsWith("HTTP Server started.", StringComparison.Ordinal))
                throw new InvalidOperationException("synthetic logging failure");
        }
    }

    private static Thread StartBackgroundServer(HttpServer server, out List<Exception> errors)
    {
        var captured = new List<Exception>();
        errors = captured;
        var thread = new Thread(() =>
        {
            try { server.Start(); }
            catch (ThreadInterruptedException) { }
            catch (Exception error) { captured.Add(error); }
        }) { IsBackground = true };
        thread.Start();
        return thread;
    }

    private static async Task AssertServerResponds(int port)
    {
        using var handler = new SocketsHttpHandler { UseProxy = false };
        using var http = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(2) };
        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (true)
        {
            try
            {
                Assert.That(await http.GetStringAsync($"http://127.0.0.1:{port}/health"), Is.EqualTo("ok"));
                return;
            }
            catch (HttpRequestException error) when (error.InnerException is SocketException && DateTime.UtcNow < deadline)
            {
                await Task.Delay(25);
            }
        }
    }

    [Test]
    [NonParallelizable]
    public void HttpMetrics_BadExternalListenerDoesNotPropagateDuringInstrumentPublication()
    {
        var throwOnNewInstrument = false;
        using var listener = new MeterListener
        {
            InstrumentPublished = (instrument, _) =>
            {
                if (throwOnNewInstrument && instrument.Meter.Name == "QaaS.Mocker.Http")
                    throw new InvalidOperationException("synthetic observer failure");
            }
        };
        listener.Start();
        throwOnNewInstrument = true;

        Assert.That(HttpRequestMetrics.TryCreatePublisher(), Is.Null);
    }

    private static HttpServer CreateServer()
    {
        return CreateServer(new HttpServerConfig
        {
            Port = 0,
            IsLocalhost = true,
            Endpoints = CreateEndpoints()
        });
    }

    private static HttpServer CreateServer(HttpServerConfig config, ILogger? logger = null)
    {
        return new HttpServer(
            config,
            logger ?? Globals.Logger,
            [
                CreateStub("MainStub", _ => CreateResponse("ok")),
                CreateStub(Constants.DefaultNotFoundTransactionStubLabel, _ => CreateResponse("fallback")),
                CreateStub(Constants.DefaultInternalErrorTransactionStubLabel, _ => CreateResponse("fallback"))
            ]);
    }

    private static HttpEndpointConfig[] CreateEndpoints()
    {
        return
        [
            new HttpEndpointConfig
            {
                Path = "/health",
                Actions =
                [
                    new HttpEndpointActionConfig
                    {
                        Name = "Health",
                        Method = HttpMethod.Get,
                        TransactionStubName = "MainStub"
                    }
                ]
            }
        ];
    }

    private static async Task InvokeHandleTransactionAsync(HttpServer server, HttpContext context, HttpRequestMetrics? metrics = null)
    {
        var task = (Task)typeof(HttpServer)
            .GetMethod("HandleTransactionAsync", BindingFlags.NonPublic | BindingFlags.Instance)!
            .Invoke(server, [context, metrics])!;

        await task;
    }

    private static IHost InvokeBuildHost(HttpServer server)
    {
        return (IHost)typeof(HttpServer)
            .GetMethod("BuildHost", BindingFlags.NonPublic | BindingFlags.Instance)!
            .Invoke(server, [null])!;
    }

    private static TransactionStub CreateStub(string name, Func<Data<object>, Data<object>> process)
    {
        return new TransactionStub
        {
            Name = name,
            Processor = new DelegateProcessor(process),
            DataSourceList = ImmutableList<DataSource>.Empty
        };
    }

    private static Data<object> CreateResponse(string payload, int statusCode = StatusCodes.Status200OK)
    {
        return new Data<object>
        {
            Body = Encoding.UTF8.GetBytes(payload),
            MetaData = new MetaData { Http = new Http { StatusCode = statusCode } }
        };
    }

    private static string CreateCertificateFile()
    {
        using var rsa = RSA.Create(2048);
        var request = new CertificateRequest("CN=localhost", rsa, HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1);
        using var certificate = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1),
            DateTimeOffset.UtcNow.AddDays(1));

        var path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.pfx");
        File.WriteAllBytes(path, certificate.Export(X509ContentType.Pfx, "password"));
        return path;
    }

    private static void RunServerStartUntilInterrupted(HttpServer server)
    {
        Exception? threadException = null;
        var thread = new Thread(() =>
        {
            try
            {
                server.Start();
            }
            catch (ThreadInterruptedException)
            {
            }
            catch (Exception exception)
            {
                threadException = exception;
            }
        })
        {
            IsBackground = true
        };

        thread.Start();
        Thread.Sleep(300);
        thread.Interrupt();

        Assert.That(thread.Join(TimeSpan.FromSeconds(3)), Is.True);
        Assert.That(threadException, Is.Null);
    }

    private sealed class DelegateProcessor(Func<Data<object>, Data<object>> process) : ITransactionProcessor
    {
        public Context Context { get; set; } = null!;
        public List<ValidationResult>? LoadAndValidateConfiguration(IConfiguration configuration) => [];
        public Data<object> Process(IImmutableList<DataSource> dataSourceList, Data<object> requestData) => process(requestData);
    }
}
