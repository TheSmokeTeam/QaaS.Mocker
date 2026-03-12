using System.ComponentModel.DataAnnotations;
using NUnit.Framework;
using QaaS.Mocker.Servers.ConfigurationObjects.GrpcServerConfigs;

namespace QaaS.Mocker.Servers.Tests.ConfigurationTests;

[TestFixture]
public class GrpcServerConfigValidationTests
{
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

            Assert.That(results.Any(result => result.MemberNames.Contains(nameof(GrpcServerConfig.CertificatePath))),
                Is.False);
        }
        finally
        {
            File.Delete(certificatePath);
        }
    }

    private static GrpcServerConfig CreateValidConfig()
    {
        return new GrpcServerConfig
        {
            Port = 50051,
            Services =
            [
                new GrpcServiceConfig
                {
                    ServiceName = "EchoService",
                    ProtoNamespace = "QaaS.Mocker.Example.Grpc",
                    AssemblyName = "QaaS.Mocker.Example",
                    Actions =
                    [
                        new GrpcEndpointActionConfig
                        {
                            Name = "EchoAction",
                            RpcName = "Echo",
                            TransactionStubName = "EchoStub"
                        }
                    ]
                }
            ]
        };
    }

    private static List<ValidationResult> Validate(GrpcServerConfig config)
    {
        var context = new ValidationContext(config);
        var results = new List<ValidationResult>();
        _ = Validator.TryValidateObject(config, context, results, validateAllProperties: true);
        return results;
    }
}
