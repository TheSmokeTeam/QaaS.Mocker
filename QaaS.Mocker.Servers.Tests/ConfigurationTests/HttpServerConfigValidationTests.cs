using System.ComponentModel.DataAnnotations;
using NUnit.Framework;
using QaaS.Mocker.Servers.ConfigurationObjects.HttpServerConfigs;
using HttpMethod = QaaS.Mocker.Servers.ConfigurationObjects.HttpServerConfigs.HttpMethod;

namespace QaaS.Mocker.Servers.Tests.ConfigurationTests;

[TestFixture]
public class HttpServerConfigValidationTests
{
    [Test]
    public void Validate_WithValidEndpoints_ReturnsSuccess()
    {
        var config = new HttpServerConfig
        {
            Port = 8080,
            Endpoints =
            [
                new HttpEndpointConfig
                {
                    Path = "/users/{id}",
                    Actions =
                    [
                        new HttpEndpointActionConfig
                        {
                            Name = "GetUser",
                            Method = HttpMethod.Get,
                            TransactionStubName = "StubA"
                        }
                    ]
                },
                new HttpEndpointConfig
                {
                    Path = "/users",
                    Actions =
                    [
                        new HttpEndpointActionConfig
                        {
                            Name = "CreateUser",
                            Method = HttpMethod.Post,
                            TransactionStubName = "StubB"
                        }
                    ]
                }
            ]
        };

        var results = Validate(config);

        Assert.That(results, Is.Empty);
    }

    [Test]
    public void Validate_WithDuplicateActionNames_ReturnsValidationError()
    {
        var config = new HttpServerConfig
        {
            Port = 8080,
            Endpoints =
            [
                new HttpEndpointConfig
                {
                    Path = "/users",
                    Actions =
                    [
                        new HttpEndpointActionConfig
                        {
                            Name = "ActionA",
                            Method = HttpMethod.Get,
                            TransactionStubName = "StubA"
                        }
                    ]
                },
                new HttpEndpointConfig
                {
                    Path = "/orders",
                    Actions =
                    [
                        new HttpEndpointActionConfig
                        {
                            Name = "actiona",
                            Method = HttpMethod.Get,
                            TransactionStubName = "StubB"
                        }
                    ]
                }
            ]
        };

        var results = Validate(config);

        Assert.That(results.Any(result => result.ErrorMessage != null &&
                                          result.ErrorMessage.Contains("Duplication")), Is.True);
    }

    [Test]
    public void Validate_WithConflictingPaths_ReturnsValidationError()
    {
        var config = new HttpServerConfig
        {
            Port = 8080,
            Endpoints =
            [
                new HttpEndpointConfig
                {
                    Path = "/users/{id}",
                    Actions =
                    [
                        new HttpEndpointActionConfig
                        {
                            Name = "ActionA",
                            Method = HttpMethod.Get,
                            TransactionStubName = "StubA"
                        }
                    ]
                },
                new HttpEndpointConfig
                {
                    Path = "/users/me",
                    Actions =
                    [
                        new HttpEndpointActionConfig
                        {
                            Name = "ActionB",
                            Method = HttpMethod.Get,
                            TransactionStubName = "StubB"
                        }
                    ]
                }
            ]
        };

        var results = Validate(config);

        Assert.That(results.Any(result => result.ErrorMessage != null &&
                                          result.ErrorMessage.Contains("conflicting", StringComparison.OrdinalIgnoreCase)), Is.True);
    }

    private static List<ValidationResult> Validate(HttpServerConfig config)
    {
        var context = new ValidationContext(config);
        var results = new List<ValidationResult>();
        _ = Validator.TryValidateObject(config, context, results, validateAllProperties: true);
        return results;
    }
}
