using NUnit.Framework;
using QaaS.Mocker.Servers.ConfigurationObjects.HttpServerConfigs;

namespace QaaS.Mocker.Servers.Tests.ConfigurationTests;

[TestFixture]
public class HttpEndpointConfigTests
{
    [Test]
    public void FixedPath_NormalizesCaseAndTrailingSlash()
    {
        var endpoint = new HttpEndpointConfig
        {
            Path = "/Users/{Id}/"
        };

        Assert.That(endpoint.FixedPath, Is.EqualTo("/users/{id}"));
    }

    [TestCase("/")]
    [TestCase("/users")]
    [TestCase("/users/{id}")]
    [TestCase("/users/{id}/orders/{orderId}")]
    public void IsPathValid_WithValidPaths_ReturnsTrue(string path)
    {
        var endpoint = new HttpEndpointConfig { Path = path };

        Assert.That(endpoint.IsPathValid(), Is.True);
    }

    [TestCase("")]
    [TestCase("users/no-leading-slash")]
    [TestCase("/users//double-slash")]
    [TestCase("/users/{id}-{other}")]
    public void IsPathValid_WithInvalidPaths_ReturnsFalse(string path)
    {
        var endpoint = new HttpEndpointConfig { Path = path };

        Assert.That(endpoint.IsPathValid(), Is.False);
    }

    [Test]
    public void GeneratePathRegex_WithParameters_MatchesAndCapturesValues()
    {
        var endpoint = new HttpEndpointConfig
        {
            Path = "/users/{id}/orders/{orderId}"
        };

        var regex = endpoint.GeneratePathRegex();
        var match = regex.Match("/users/42/orders/A1");

        Assert.Multiple(() =>
        {
            Assert.That(match.Success, Is.True);
            Assert.That(match.Groups["id"].Value, Is.EqualTo("42"));
            Assert.That(match.Groups["orderid"].Value, Is.EqualTo("A1"));
        });
    }

    [Test]
    public void GeneratePathRegex_WithDuplicateParameterName_ThrowsArgumentException()
    {
        var endpoint = new HttpEndpointConfig
        {
            Path = "/users/{id}/orders/{id}"
        };

        Assert.Throws<ArgumentException>(() => endpoint.GeneratePathRegex());
    }

    [Test]
    public void GeneratePathRegex_WithMultipleParametersInSameSegment_ThrowsNotSupportedException()
    {
        var endpoint = new HttpEndpointConfig
        {
            Path = "/users/{id}{other}"
        };

        Assert.Throws<NotSupportedException>(() => endpoint.GeneratePathRegex());
    }

    [Test]
    public void GenerateDummyPath_ReplacesParametersWithPlaceholder()
    {
        var endpoint = new HttpEndpointConfig
        {
            Path = "/users/{id}/orders/{orderId}"
        };

        var dummyPath = endpoint.GenerateDummyPath();

        Assert.That(dummyPath, Is.EqualTo("/users/placeholder/orders/placeholder"));
    }
}
