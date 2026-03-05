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
