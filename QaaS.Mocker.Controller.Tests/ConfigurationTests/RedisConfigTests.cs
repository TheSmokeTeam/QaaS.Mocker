using NUnit.Framework;
using QaaS.Mocker.Controller.ConfigurationObjects;

namespace QaaS.Mocker.Controller.Tests.ConfigurationTests;

[TestFixture]
public class RedisConfigTests
{
    [Test]
    public void CreateRedisConfigurationOptions_MapsConfiguredValues()
    {
        var config = new RedisConfig
        {
            Host = "localhost:6379",
            Username = "user",
            Password = "pass",
            AbortOnConnectFail = false,
            ConnectRetry = 7,
            ClientName = "client-a",
            AsyncTimeout = 123,
            Ssl = true,
            SslHost = "localhost",
            KeepAlive = 99
        };

        var options = config.CreateRedisConfigurationOptions();

        Assert.Multiple(() =>
        {
            Assert.That(options.EndPoints.Select(endpoint => endpoint.ToString()),
                Has.Some.Contains("localhost:6379"));
            Assert.That(options.User, Is.EqualTo("user"));
            Assert.That(options.Password, Is.EqualTo("pass"));
            Assert.That(options.AbortOnConnectFail, Is.False);
            Assert.That(options.ConnectRetry, Is.EqualTo(7));
            Assert.That(options.ClientName, Is.EqualTo("client-a"));
            Assert.That(options.AsyncTimeout, Is.EqualTo(123));
            Assert.That(options.Ssl, Is.True);
            Assert.That(options.SslHost, Is.EqualTo("localhost"));
            Assert.That(options.KeepAlive, Is.EqualTo(99));
        });
    }
}
