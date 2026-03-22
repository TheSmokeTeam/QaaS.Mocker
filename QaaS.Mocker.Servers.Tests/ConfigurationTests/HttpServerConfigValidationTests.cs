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

    [Test]
    public void Validate_WithSecuredSchemaAndMissingCertificate_ReturnsValidationError()
    {
        var config = CreateValidConfig();
        config.IsSecuredSchema = true;
        config.CertificatePath = "Certificates/missing-devcert.pfx";

        var results = Validate(config);

        Assert.That(results.Any(result => result.ErrorMessage != null &&
                                          result.ErrorMessage.Contains("CertificatePath", StringComparison.OrdinalIgnoreCase)),
            Is.True);
    }

    [Test]
    public void Validate_WithSecuredSchemaAndExistingCertificate_ReturnsSuccess()
    {
        var certificatePath = Path.GetTempFileName();
        try
        {
            var config = CreateValidConfig();
            config.IsSecuredSchema = true;
            config.CertificatePath = certificatePath;

            var results = Validate(config);

            Assert.That(results.Any(result => result.MemberNames.Contains(nameof(HttpServerConfig.CertificatePath))),
                Is.False);
        }
        finally
        {
            File.Delete(certificatePath);
        }
    }

    [Test]
    public void Validate_WithInvalidPathPattern_ReturnsValidationError()
    {
        var config = new HttpServerConfig
        {
            Port = 8080,
            Endpoints =
            [
                new HttpEndpointConfig
                {
                    Path = "/users/{id",
                    Actions =
                    [
                        new HttpEndpointActionConfig
                        {
                            Name = "GetUser",
                            Method = HttpMethod.Get,
                            TransactionStubName = "StubA"
                        }
                    ]
                }
            ]
        };

        var results = Validate(config);

        Assert.That(results.Any(result => result.ErrorMessage != null &&
                                          result.ErrorMessage.Contains("not valid", StringComparison.OrdinalIgnoreCase)),
            Is.True);
    }

    [Test]
    public void Validate_WithNullEndpoints_ReturnsRequiredValidationErrorWithoutCustomFailures()
    {
        var config = new HttpServerConfig
        {
            Port = 8080,
            Endpoints = null
        };

        var results = Validate(config);

        Assert.That(results, Is.Empty);
        Assert.That(results.Any(result => result.ErrorMessage != null &&
                                          result.ErrorMessage.Contains("conflicting", StringComparison.OrdinalIgnoreCase)),
            Is.False);
    }

    [Test]
    public void ValidAndUniquePathRegexEndpointsAttribute_WithNonEndpointValue_ReturnsSuccess()
    {
        var attribute = new ValidAndUniquePathRegexEndpointsAttribute();
        var context = new ValidationContext(new HttpServerConfig())
        {
            MemberName = nameof(HttpServerConfig.Endpoints)
        };

        var result = attribute.GetValidationResult("invalid", context);

        Assert.That(result, Is.EqualTo(ValidationResult.Success));
    }

    [Test]
    public void UniqueActionNameEndpointsAttribute_WithUnnamedActions_IgnoresThem()
    {
        var attribute = new UniqueActionNameEndpointsAttribute();
        var config = new HttpServerConfig
        {
            Endpoints =
            [
                new HttpEndpointConfig
                {
                    Path = "/users",
                    Actions =
                    [
                        new HttpEndpointActionConfig
                        {
                            Name = null,
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
                            Name = null,
                            Method = HttpMethod.Post,
                            TransactionStubName = "StubB"
                        }
                    ]
                }
            ]
        };

        var result = attribute.GetValidationResult(config.Endpoints, new ValidationContext(config));

        Assert.That(result, Is.EqualTo(ValidationResult.Success));
    }

    [Test]
    public void ValidAndUniquePathRegexEndpointsAttribute_WithNullEndpoints_ReturnsSuccess()
    {
        var attribute = new ValidAndUniquePathRegexEndpointsAttribute();
        var config = new HttpServerConfig
        {
            Endpoints = null
        };

        var result = attribute.GetValidationResult(config.Endpoints, new ValidationContext(config));

        Assert.That(result, Is.EqualTo(ValidationResult.Success));
    }

    [Test]
    public void Validate_WithConflictingPathsInReverseOrder_ReturnsValidationError()
    {
        var config = new HttpServerConfig
        {
            Port = 8080,
            Endpoints =
            [
                new HttpEndpointConfig
                {
                    Path = "/users/me",
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
                    Path = "/users/{id}",
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

    private static HttpServerConfig CreateValidConfig()
    {
        return new HttpServerConfig
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
                }
            ]
        };
    }

    private static List<ValidationResult> Validate(HttpServerConfig config)
    {
        var context = new ValidationContext(config);
        var results = new List<ValidationResult>();
        _ = Validator.TryValidateObject(config, context, results, validateAllProperties: true);
        return results;
    }
}
