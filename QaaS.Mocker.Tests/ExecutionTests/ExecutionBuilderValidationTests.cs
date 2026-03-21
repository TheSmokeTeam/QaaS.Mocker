using System.ComponentModel.DataAnnotations;
using NUnit.Framework;
using QaaS.Mocker.Servers.ConfigurationObjects;
using QaaS.Mocker.Servers.ConfigurationObjects.GrpcServerConfigs;
using QaaS.Mocker.Servers.ConfigurationObjects.HttpServerConfigs;
using QaaS.Mocker.Servers.ConfigurationObjects.SocketServerConfigs;
using HttpMethod = QaaS.Mocker.Servers.ConfigurationObjects.HttpServerConfigs.HttpMethod;

namespace QaaS.Mocker.Tests.ExecutionTests;

[TestFixture]
public class ExecutionBuilderValidationTests
{
    [Test]
    public void Validate_WithGrpcFallbackActionNameMatchingSocketAction_ReturnsDuplicateError()
    {
        var builder = new ExecutionBuilder
        {
            Servers =
            [
                BuildGrpcServer(serviceName: "EchoService", rpcName: "Echo", actionName: null),
                BuildSocketServer("echoservice.echo")
            ]
        };

        var results = builder.Validate(new ValidationContext(builder)).ToArray();

        Assert.Multiple(() =>
        {
            Assert.That(results, Has.Length.EqualTo(1));
            Assert.That(results[0].ErrorMessage, Does.Contain("EchoService.Echo"));
        });
    }

    [Test]
    public void Validate_WithWhitespaceAndMissingActionNames_IgnoresUnnamedActions()
    {
        var builder = new ExecutionBuilder
        {
            Servers =
            [
                new ServerConfig
                {
                    Http = new HttpServerConfig
                    {
                        Port = 18081,
                        Endpoints =
                        [
                            new HttpEndpointConfig
                            {
                                Path = "/health",
                                Actions =
                                [
                                    new HttpEndpointActionConfig
                                    {
                                        Name = "   ",
                                        Method = HttpMethod.Get,
                                        TransactionStubName = "StubA"
                                    }
                                ]
                            }
                        ]
                    }
                },
                new ServerConfig
                {
                    Socket = new SocketServerConfig
                    {
                        Endpoints =
                        [
                            new SocketEndpointConfig
                            {
                                Port = 19090,
                                ProtocolType = System.Net.Sockets.ProtocolType.Tcp,
                                SocketType = System.Net.Sockets.SocketType.Stream,
                                TimeoutMs = 1000,
                                BufferSizeBytes = 2048,
                                Action = new SocketActionConfig
                                {
                                    Name = null,
                                    Method = SocketMethod.Collect,
                                    TransactionStubName = "StubA"
                                }
                            }
                        ]
                    }
                }
            ]
        };

        var results = builder.Validate(new ValidationContext(builder)).ToArray();

        Assert.That(results, Is.Empty);
    }

    private static ServerConfig BuildGrpcServer(string serviceName, string rpcName, string? actionName)
    {
        return new ServerConfig
        {
            Grpc = new GrpcServerConfig
            {
                Port = 50051,
                Services =
                [
                    new GrpcServiceConfig
                    {
                        ServiceName = serviceName,
                        ProtoNamespace = "Tests",
                        AssemblyName = "Tests",
                        Actions =
                        [
                            new GrpcEndpointActionConfig
                            {
                                Name = actionName,
                                RpcName = rpcName,
                                TransactionStubName = "StubA"
                            }
                        ]
                    }
                ]
            }
        };
    }

    private static ServerConfig BuildSocketServer(string actionName)
    {
        return new ServerConfig
        {
            Socket = new SocketServerConfig
            {
                Endpoints =
                [
                    new SocketEndpointConfig
                    {
                        Port = 19090,
                        ProtocolType = System.Net.Sockets.ProtocolType.Tcp,
                        SocketType = System.Net.Sockets.SocketType.Stream,
                        TimeoutMs = 1000,
                        BufferSizeBytes = 2048,
                        Action = new SocketActionConfig
                        {
                            Name = actionName,
                            Method = SocketMethod.Collect,
                            TransactionStubName = "StubA"
                        }
                    }
                ]
            }
        };
    }
}
