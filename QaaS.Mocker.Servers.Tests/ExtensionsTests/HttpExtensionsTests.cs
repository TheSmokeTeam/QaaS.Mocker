using System.Text;
using Microsoft.AspNetCore.Http;
using NUnit.Framework;
using QaaS.Framework.SDK.Session.DataObjects;
using QaaS.Framework.SDK.Session.MetaDataObjects;
using QaaS.Mocker.Servers.Extensions;
using HttpMethod = QaaS.Mocker.Servers.ConfigurationObjects.HttpServerConfigs.HttpMethod;

namespace QaaS.Mocker.Servers.Tests.ExtensionsTests;

[TestFixture]
public class HttpExtensionsTests
{
    [TestCase("GET", HttpMethod.Get)]
    [TestCase("post", HttpMethod.Post)]
    [TestCase("PuT", HttpMethod.Put)]
    [TestCase("DELETE", HttpMethod.Delete)]
    [TestCase("HEAD", HttpMethod.Head)]
    [TestCase("OPTIONS", HttpMethod.Options)]
    [TestCase("PATCH", HttpMethod.Patch)]
    [TestCase("TRACE", HttpMethod.Trace)]
    [TestCase("CONNECT", HttpMethod.Connect)]
    public void ToHttpMethodEnum_WithSupportedMethod_ReturnsMappedEnum(string input, HttpMethod expected)
    {
        var result = input.ToHttpMethodEnum();

        Assert.That(result, Is.EqualTo(expected));
    }

    [Test]
    public void ToHttpMethodEnum_WithUnsupportedMethod_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => "BREW".ToHttpMethodEnum());
    }

    [TestCase("")]
    [TestCase("X")]
    [TestCase("BAD")]
    [TestCase("BREWER")]
    [TestCase("WHATEVER")]
    public void ToHttpMethodEnum_WithUnsupportedMethodShapes_ThrowsArgumentException(string input)
    {
        Assert.Throws<ArgumentException>(() => input.ToHttpMethodEnum());
    }

    [TestCase("GEQ")]
    [TestCase("POZT")]
    [TestCase("PAXCH")]
    [TestCase("DELETF")]
    [TestCase("HEAQ")]
    [TestCase("OPTIONA")]
    [TestCase("TRACG")]
    [TestCase("CONNECX")]
    [TestCase("GETT")]
    [TestCase("HEADS")]
    [TestCase("PATCHS")]
    [TestCase("TRACES")]
    public void ToHttpMethodEnum_WithNearMatchUnsupportedMethod_ThrowsArgumentException(string input)
    {
        Assert.Throws<ArgumentException>(() => input.ToHttpMethodEnum());
    }

    [Test]
    public async Task ConstructRequestDataAsync_CopiesBodyHeadersAndUri()
    {
        var context = new DefaultHttpContext();
        context.Request.Method = "POST";
        context.Request.Scheme = "https";
        context.Request.Host = new HostString("localhost", 8443);
        context.Request.Path = "/health";
        context.Request.QueryString = new QueryString("?a=1");
        context.Request.Headers["x-test"] = "value";
        context.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes("payload"));

        var data = await context.Request.ConstructRequestDataAsync();

        Assert.Multiple(() =>
        {
            Assert.That(data.Body, Is.TypeOf<byte[]>());
            Assert.That(Encoding.UTF8.GetString((byte[])data.Body!), Is.EqualTo("payload"));
            Assert.That(data.MetaData?.Http?.RequestHeaders?["x-test"], Is.EqualTo("value"));
            Assert.That(data.MetaData?.Http?.Uri?.ToString(), Is.EqualTo("https://localhost:8443/health?a=1"));
        });
    }

    [Test]
    public async Task HandleResponseDataAndCloseAsync_WithNonHeadMethod_WritesBodyAndHeaders()
    {
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();

        var responseData = new Data<object>
        {
            Body = Encoding.UTF8.GetBytes("ok"),
            MetaData = new MetaData
            {
                Http = new Http
                {
                    StatusCode = 201,
                    ResponseHeaders = new Dictionary<string, string> { ["x-response"] = "one" },
                    Headers = new Dictionary<string, string> { ["x-legacy"] = "two" }
                }
            }
        };

        await context.Response.HandleResponseDataAndCloseAsync(responseData, HttpMethod.Get);

        context.Response.Body.Position = 0;
        using var reader = new StreamReader(context.Response.Body, Encoding.UTF8, leaveOpen: true);
        var body = await reader.ReadToEndAsync();

        Assert.Multiple(() =>
        {
            Assert.That(context.Response.StatusCode, Is.EqualTo(201));
            Assert.That(context.Response.Headers["x-response"].ToString(), Is.EqualTo("one"));
            Assert.That(context.Response.Headers["x-legacy"].ToString(), Is.EqualTo("two"));
            Assert.That(body, Is.EqualTo("ok"));
        });
    }

    [Test]
    public async Task HandleResponseDataAndCloseAsync_WithOnlyResponseHeaders_AppliesHeaders()
    {
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();

        var responseData = new Data<object>
        {
            Body = Array.Empty<byte>(),
            MetaData = new MetaData
            {
                Http = new Http
                {
                    ResponseHeaders = new Dictionary<string, string> { ["x-response"] = "one" }
                }
            }
        };

        await context.Response.HandleResponseDataAndCloseAsync(responseData, HttpMethod.Get);

        Assert.Multiple(() =>
        {
            Assert.That(context.Response.StatusCode, Is.EqualTo(StatusCodes.Status200OK));
            Assert.That(context.Response.Headers["x-response"].ToString(), Is.EqualTo("one"));
            Assert.That(context.Response.Headers.ContainsKey("x-legacy"), Is.False);
        });
    }

    [Test]
    public async Task HandleResponseDataAndCloseAsync_WithOnlyLegacyHeaders_AppliesHeaders()
    {
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();

        var responseData = new Data<object>
        {
            Body = Array.Empty<byte>(),
            MetaData = new MetaData
            {
                Http = new Http
                {
                    Headers = new Dictionary<string, string> { ["x-legacy"] = "two" }
                }
            }
        };

        await context.Response.HandleResponseDataAndCloseAsync(responseData, HttpMethod.Get);

        Assert.Multiple(() =>
        {
            Assert.That(context.Response.StatusCode, Is.EqualTo(StatusCodes.Status200OK));
            Assert.That(context.Response.Headers.ContainsKey("x-response"), Is.False);
            Assert.That(context.Response.Headers["x-legacy"].ToString(), Is.EqualTo("two"));
        });
    }

    [Test]
    public async Task HandleResponseDataAndCloseAsync_WithHeadMethod_DoesNotWriteBody()
    {
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();

        var responseData = new Data<object>
        {
            Body = Encoding.UTF8.GetBytes("should-not-be-written"),
            MetaData = new MetaData { Http = new Http { StatusCode = 204 } }
        };

        await context.Response.HandleResponseDataAndCloseAsync(responseData, HttpMethod.Head);

        Assert.That(context.Response.Body.Length, Is.EqualTo(0));
    }

    [Test]
    public async Task HandleResponseDataAndCloseAsync_WithHttpMetadataAndNoHeaders_UsesDefaultStatusCode()
    {
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();

        await context.Response.HandleResponseDataAndCloseAsync(
            new Data<object>
            {
                Body = Array.Empty<byte>(),
                MetaData = new MetaData { Http = new Http() }
            },
            HttpMethod.Get);

        Assert.Multiple(() =>
        {
            Assert.That(context.Response.StatusCode, Is.EqualTo(StatusCodes.Status200OK));
            Assert.That(context.Response.Headers, Is.Empty);
            Assert.That(context.Response.Body.Length, Is.EqualTo(0));
        });
    }

    [Test]
    public async Task HandleResponseDataAndCloseAsync_WithMetadataWithoutHttpObject_UsesDefaults()
    {
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();

        await context.Response.HandleResponseDataAndCloseAsync(
            new Data<object>
            {
                Body = Array.Empty<byte>(),
                MetaData = new MetaData { Http = null! }
            },
            HttpMethod.Get);

        Assert.Multiple(() =>
        {
            Assert.That(context.Response.StatusCode, Is.EqualTo(StatusCodes.Status200OK));
            Assert.That(context.Response.Headers, Is.Empty);
            Assert.That(context.Response.Body.Length, Is.EqualTo(0));
        });
    }

    [Test]
    public async Task HandleResponseDataAndCloseAsync_WithoutHttpMetadata_UsesDefaults()
    {
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();

        await context.Response.HandleResponseDataAndCloseAsync(
            new Data<object> { Body = "not-bytes" },
            HttpMethod.Get);

        context.Response.Body.Position = 0;
        using var reader = new StreamReader(context.Response.Body, Encoding.UTF8, leaveOpen: true);
        var body = await reader.ReadToEndAsync();

        Assert.Multiple(() =>
        {
            Assert.That(context.Response.StatusCode, Is.EqualTo(StatusCodes.Status200OK));
            Assert.That(context.Response.Headers, Is.Empty);
            Assert.That(body, Is.Empty);
        });
    }
}
