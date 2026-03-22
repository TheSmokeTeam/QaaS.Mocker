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

    [Test]
    public void Validate_WithNonEndpointValue_SkipsCustomSocketValidators()
    {
        var duplicateActionValidator = new ValidationContext(new SocketServerConfig())
        {
            MemberName = nameof(SocketServerConfig.Endpoints)
        };

        var duplicateActionAttribute = new UniqueActionNameInAllEndpointsAttribute();
        var broadcastAttribute = new BroadcastOverUdpNotSupportedAttribute();
        var socketTypeAttribute = new SocketTypeMatchesProtocolAttribute();

        Assert.Multiple(() =>
        {
            Assert.That(duplicateActionAttribute.GetValidationResult("invalid", duplicateActionValidator),
                Is.EqualTo(ValidationResult.Success));
            Assert.That(broadcastAttribute.GetValidationResult("invalid", duplicateActionValidator),
                Is.EqualTo(ValidationResult.Success));
            Assert.That(socketTypeAttribute.GetValidationResult("invalid", duplicateActionValidator),
                Is.EqualTo(ValidationResult.Success));
        });
    }

    [Test]
    public void Validate_WithNullEndpoints_ReturnsRequiredValidationErrorWithoutCustomFailures()
    {
        var config = new SocketServerConfig
        {
            Endpoints = null
        };

        var results = Validate(config);

        Assert.That(results.Any(result => result.MemberNames.Contains(nameof(SocketServerConfig.Endpoints))), Is.True);
        Assert.That(results.Any(result => result.ErrorMessage != null &&
                                          result.ErrorMessage.Contains("Duplication", StringComparison.OrdinalIgnoreCase)),
            Is.False);
    }

    [Test]
    public void Validate_WithUnnamedActions_IgnoresDuplicateCheck()
    {
        var attribute = new UniqueActionNameInAllEndpointsAttribute();
        var config = new SocketServerConfig
        {
            Endpoints =
            [
                CreateEndpoint(7001, null!),
                CreateEndpoint(7002, null!)
            ]
        };

        var result = attribute.GetValidationResult(config.Endpoints, new ValidationContext(config));

        Assert.That(result, Is.EqualTo(ValidationResult.Success));
    }

    [Test]
    public void Validate_WithNullEndpointsOnDuplicateActionAttribute_ReturnsSuccess()
    {
        var attribute = new UniqueActionNameInAllEndpointsAttribute();
        var config = new SocketServerConfig
        {
            Endpoints = null
        };

        var result = attribute.GetValidationResult(config.Endpoints, new ValidationContext(config));

        Assert.That(result, Is.EqualTo(ValidationResult.Success));
    }

    [Test]
    public void Validate_WithNonNullValueAndNullEndpointsOnDuplicateActionAttribute_ReturnsSuccess()
    {
        var attribute = new UniqueActionNameInAllEndpointsAttribute();
        var config = new SocketServerConfig
        {
            Endpoints = null
        };

        var result = attribute.GetValidationResult(Array.Empty<SocketEndpointConfig>(), new ValidationContext(config));

        Assert.That(result, Is.EqualTo(ValidationResult.Success));
    }

    [Test]
    public void Validate_WithNullActions_IgnoresDuplicateCheck()
    {
        var attribute = new UniqueActionNameInAllEndpointsAttribute();
        var config = new SocketServerConfig
        {
            Endpoints =
            [
                new SocketEndpointConfig
                {
                    Port = 7001,
                    ProtocolType = ProtocolType.Tcp,
                    SocketType = SocketType.Stream,
                    Action = null
                }
            ]
        };

        var result = attribute.GetValidationResult(config.Endpoints, new ValidationContext(config));

        Assert.That(result, Is.EqualTo(ValidationResult.Success));
    }

    [Test]
    public void Validate_WithUdpBroadcastEndpointWithoutPort_IgnoresMissingPort()
    {
        var attribute = new BroadcastOverUdpNotSupportedAttribute();
        var config = new SocketServerConfig
        {
            Endpoints =
            [
                new SocketEndpointConfig
                {
                    Port = null,
                    ProtocolType = ProtocolType.Udp,
                    SocketType = SocketType.Dgram,
                    Action = new SocketActionConfig
                    {
                        Name = "BroadcastA",
                        Method = SocketMethod.Broadcast
                    }
                }
            ]
        };

        var result = attribute.GetValidationResult(config.Endpoints, new ValidationContext(config));

        Assert.That(result, Is.EqualTo(ValidationResult.Success));
    }

    [Test]
    public void Validate_WithUdpEndpointWithoutAction_IgnoresBroadcastCheck()
    {
        var attribute = new BroadcastOverUdpNotSupportedAttribute();
        var config = new SocketServerConfig
        {
            Endpoints =
            [
                new SocketEndpointConfig
                {
                    Port = 7001,
                    ProtocolType = ProtocolType.Udp,
                    SocketType = SocketType.Dgram,
                    Action = null
                }
            ]
        };

        var result = attribute.GetValidationResult(config.Endpoints, new ValidationContext(config));

        Assert.That(result, Is.EqualTo(ValidationResult.Success));
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
