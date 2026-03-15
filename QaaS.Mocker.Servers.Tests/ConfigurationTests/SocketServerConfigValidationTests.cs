using System.ComponentModel.DataAnnotations;
using System.Net.Sockets;
using NUnit.Framework;
using QaaS.Mocker.Servers.ConfigurationObjects.SocketServerConfigs;

namespace QaaS.Mocker.Servers.Tests.ConfigurationTests;

[TestFixture]
public class SocketServerConfigValidationTests
{
    [Test]
    public void Validate_WithUniqueActionNames_ReturnsSuccess()
    {
        var config = new SocketServerConfig
        {
            Endpoints =
            [
                CreateEndpoint(7001, "ActionA"),
                CreateEndpoint(7002, "ActionB")
            ]
        };

        var results = Validate(config);

        Assert.That(results, Is.Empty);
    }

    [Test]
    public void Validate_WithDuplicateActionNames_ReturnsValidationError()
    {
        var config = new SocketServerConfig
        {
            Endpoints =
            [
                CreateEndpoint(7001, "ActionA"),
                CreateEndpoint(7002, "actiona")
            ]
        };

        var results = Validate(config);

        Assert.That(results.Any(result => result.ErrorMessage != null &&
                                          result.ErrorMessage.Contains("Duplication")), Is.True);
    }

    [Test]
    public void Validate_WithUdpBroadcastEndpoint_ReturnsValidationError()
    {
        var config = new SocketServerConfig
        {
            Endpoints =
            [
                new SocketEndpointConfig
                {
                    Port = 7001,
                    ProtocolType = ProtocolType.Udp,
                    SocketType = SocketType.Dgram,
                    TimeoutMs = 100,
                    Action = new SocketActionConfig
                    {
                        Name = "BroadcastA",
                        Method = SocketMethod.Broadcast,
                        DataSourceName = "ds1"
                    }
                }
            ]
        };

        var results = Validate(config);

        Assert.That(results.Any(result => result.ErrorMessage != null &&
                                          result.ErrorMessage.Contains("do not support UDP")), Is.True);
    }

    [Test]
    public void Validate_WithUdpStreamSocket_ReturnsValidationError()
    {
        var config = new SocketServerConfig
        {
            Endpoints =
            [
                new SocketEndpointConfig
                {
                    Port = 7001,
                    ProtocolType = ProtocolType.Udp,
                    SocketType = SocketType.Stream,
                    TimeoutMs = 100,
                    Action = new SocketActionConfig
                    {
                        Name = "CollectA",
                        Method = SocketMethod.Collect
                    }
                }
            ]
        };

        var results = Validate(config);

        Assert.That(results.Any(result => result.ErrorMessage != null &&
                                          result.ErrorMessage.Contains("protocol and socket type must match")), Is.True);
    }

    private static SocketEndpointConfig CreateEndpoint(int port, string actionName)
    {
        return new SocketEndpointConfig
        {
            Port = port,
            ProtocolType = ProtocolType.Tcp,
            TimeoutMs = 100,
            Action = new SocketActionConfig
            {
                Name = actionName,
                Method = SocketMethod.Collect
            }
        };
    }

    private static List<ValidationResult> Validate(SocketServerConfig config)
    {
        var context = new ValidationContext(config);
        var results = new List<ValidationResult>();
        _ = Validator.TryValidateObject(config, context, results, validateAllProperties: true);
        return results;
    }
}
