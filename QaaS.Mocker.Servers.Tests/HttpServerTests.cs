using System.Collections.Immutable;
using System.ComponentModel.DataAnnotations;
using System.Reflection;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Moq;
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

    [Test]
    public async Task HttpSummary_ClassifiesDispatchAndResponseIndependently()
    {
        var logger = new Mock<ILogger>();
        var identity = new MockerLogIdentity("red", "billing");
        using var metrics = new HttpRequestMetrics(logger.Object, identity, 8080);
        var endpoints = CreateEndpoints();
        endpoints[0].Actions = [.. endpoints[0].Actions, new HttpEndpointActionConfig
            { Method = HttpMethod.Post, Name = "Broken", TransactionStubName = "Broken" }];
        var server = new HttpServer(new HttpServerConfig { Endpoints = endpoints }, Globals.Logger,
        [
            CreateStub("MainStub", _ => CreateResponse("intentional", 503)),
            CreateStub("Broken", _ => throw new InvalidOperationException("original failure")),
            CreateStub(Constants.DefaultNotFoundTransactionStubLabel, _ => CreateResponse("fallback")),
            CreateStub(Constants.DefaultInternalErrorTransactionStubLabel, _ => CreateResponse("error", 202))
        ]);
        foreach (var (method, path) in new[] { ("GET", "/health"), ("GET", "/absent"), ("BREW", "/health"), ("POST", "/health") })
        {
            var context = new DefaultHttpContext();
            context.Request.Method = method;
            context.Request.Path = path;
            context.Request.Host = new HostString("localhost");
            context.Request.Scheme = "http";
            await InvokeHandleTransactionAsync(server, context, metrics);
        }
        var interruptedResponse = new DefaultHttpContext();
        interruptedResponse.Request.Method = "GET";
        interruptedResponse.Request.Path = "/health";
        interruptedResponse.Request.Scheme = "http";
        interruptedResponse.Request.Host = new HostString("localhost");
        using var readOnlyBody = new MemoryStream([], writable: false);
        interruptedResponse.Response.Body = readOnlyBody;
        await InvokeHandleTransactionAsync(server, interruptedResponse, metrics);
        metrics.Flush();
        metrics.Flush();
        var summary = logger.Invocations.Single(x => x.Method.Name == "Log");
        var values = ((IEnumerable<KeyValuePair<string, object?>>)summary.Arguments[2]).ToDictionary();
        Assert.Multiple(() =>
        {
            Assert.That(values["QaasEvent"], Is.EqualTo("MockerHttpRequests"));
            Assert.That(values["Requests"], Is.EqualTo(5));
            Assert.That(values["HandlerErrors"], Is.EqualTo(3));
            Assert.That(values["Misses"], Is.EqualTo(1));
            Assert.That(values["Http5xx"], Is.EqualTo(3));
            Assert.That(values["AttemptId"], Is.Not.Empty);
            Assert.That(values["BatchSequence"], Is.EqualTo(1L));
            Assert.That(values["ProjectKnown"], Is.False);
            Assert.That(values.ContainsKey("ExecutionId"), Is.False);
            Assert.That(values["Team"], Is.EqualTo("red"));
            Assert.That(values["System"], Is.EqualTo("billing"));
        });
    }

    [Test]
    public void HttpSummary_ConcurrentServersAndFlushesKeepSeparateTotals()
    {
        var logger = new Mock<ILogger>();
        var identity = new MockerLogIdentity("red", "billing");
        using var first = new HttpRequestMetrics(logger.Object, identity, 0);
        using var second = new HttpRequestMetrics(logger.Object, identity, 0);
        Parallel.For(0, 1_000, i =>
        {
            first.Complete(HttpRequestOutcome.Error, 500);
            second.Complete(HttpRequestOutcome.Miss, 200);
            if (i % 100 == 0)
                first.Flush();
        });
        first.Flush();
        second.Flush();

        var totals = logger.Invocations.Where(x => x.Method.Name == "Log")
            .Select(x => ((IEnumerable<KeyValuePair<string, object?>>)x.Arguments[2]).ToDictionary())
            .GroupBy(x => x["ServerId"]).Select(group => new
            {
                Sequence = group.Select(x => (long)x["BatchSequence"]!).Order().ToArray(),
                Requests = group.Sum(x => (long)x["Requests"]!),
                Errors = group.Sum(x => (long)x["HandlerErrors"]!),
                Misses = group.Sum(x => (long)x["Misses"]!)
            }).ToArray();
        Assert.That(totals, Has.Length.EqualTo(2));
        foreach (var instance in totals)
            Assert.That(instance.Sequence, Is.EqualTo(Enumerable.Range(1, instance.Sequence.Length).Select(i => (long)i)));
        Assert.That(totals.Select(x => (x.Requests, x.Errors, x.Misses)),
            Is.EquivalentTo(new[] { (1000L, 1000L, 0L), (1000L, 0L, 1000L) }));
    }

    [Test]
    public async Task HttpSummary_SkipsOverlappingWritesWithoutDrainingTheirCounts()
    {
        using var entered = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        var logger = new Mock<ILogger>();
        logger.Setup(x => x.Log(LogLevel.Debug, It.IsAny<EventId>(), It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception?>(), It.IsAny<Func<It.IsAnyType, Exception?, string>>()))
            .Callback(new InvocationAction(invocation =>
            {
                var fields = ((IEnumerable<KeyValuePair<string, object?>>)invocation.Arguments[2]).ToDictionary();
                if (Equals(fields["BatchSequence"], 1L))
                {
                    entered.Set();
                    release.Wait(TimeSpan.FromSeconds(8));
                }
            }));
        using var metrics = new HttpRequestMetrics(logger.Object, new MockerLogIdentity("red", "billing"), 8080);

        metrics.Complete(HttpRequestOutcome.Error, 500);
        var first = Task.Run(metrics.Flush);
        try
        {
            Assert.That(entered.Wait(TimeSpan.FromSeconds(3)), Is.True);
            metrics.Complete(HttpRequestOutcome.Miss, 404);
            metrics.Flush();
            Assert.That(logger.Invocations.Count(x => x.Method.Name == "Log"), Is.EqualTo(1));
        }
        finally
        {
            release.Set();
            await first.WaitAsync(TimeSpan.FromSeconds(3));
        }

        metrics.Flush();
        var fields = logger.Invocations.Where(x => x.Method.Name == "Log")
            .Select(x => ((IEnumerable<KeyValuePair<string, object?>>)x.Arguments[2]).ToDictionary()).ToArray();
        Assert.That(fields.Select(x => x["BatchSequence"]), Is.EqualTo(new object[] { 1L, 2L }));
        Assert.That(fields.Select(x => x["Requests"]), Is.EqualTo(new object[] { 1L, 1L }));
        Assert.That(fields[1]["Misses"], Is.EqualTo(1L));
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
