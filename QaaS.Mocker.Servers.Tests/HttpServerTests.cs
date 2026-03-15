using System.Collections.Immutable;
using System.ComponentModel.DataAnnotations;
using System.Reflection;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using NUnit.Framework;
using QaaS.Framework.SDK.ContextObjects;
using QaaS.Framework.SDK.DataSourceObjects;
using QaaS.Framework.SDK.Hooks.Processor;
using QaaS.Framework.SDK.Session.DataObjects;
using QaaS.Framework.SDK.Session.MetaDataObjects;
using QaaS.Mocker.Servers.ConfigurationObjects.HttpServerConfigs;
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

    private static HttpServer CreateServer()
    {
        return CreateServer(new HttpServerConfig
        {
            Port = 0,
            IsLocalhost = true,
            Endpoints = CreateEndpoints()
        });
    }

    private static HttpServer CreateServer(HttpServerConfig config)
    {
        return new HttpServer(
            config,
            Globals.Logger,
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

    private static async Task InvokeHandleTransactionAsync(HttpServer server, HttpContext context)
    {
        var task = (Task)typeof(HttpServer)
            .GetMethod("HandleTransactionAsync", BindingFlags.NonPublic | BindingFlags.Instance)!
            .Invoke(server, [context])!;

        await task;
    }

    private static IHost InvokeBuildHost(HttpServer server)
    {
        return (IHost)typeof(HttpServer)
            .GetMethod("BuildHost", BindingFlags.NonPublic | BindingFlags.Instance)!
            .Invoke(server, null)!;
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

    private static Data<object> CreateResponse(string payload)
    {
        return new Data<object>
        {
            Body = Encoding.UTF8.GetBytes(payload),
            MetaData = new MetaData { Http = new Http { StatusCode = StatusCodes.Status200OK } }
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

    private sealed class DelegateProcessor(Func<Data<object>, Data<object>> process) : ITransactionProcessor
    {
        public Context Context { get; set; } = null!;
        public List<ValidationResult>? LoadAndValidateConfiguration(IConfiguration configuration) => [];
        public Data<object> Process(IImmutableList<DataSource> dataSourceList, Data<object> requestData) => process(requestData);
    }
}
