using System.Collections.Immutable;
using System.ComponentModel.DataAnnotations;
using System.Reflection;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Google.Protobuf;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Moq;
using Moq.Protected;
using NUnit.Framework;
using QaaS.Framework.SDK.ContextObjects;
using QaaS.Framework.SDK.DataSourceObjects;
using QaaS.Framework.SDK.Hooks.Processor;
using QaaS.Framework.SDK.Session.DataObjects;
using QaaS.Mocker.Servers.ConfigurationObjects.GrpcServerConfigs;
using QaaS.Mocker.Servers.Servers;
using QaaS.Mocker.Stubs;
using QaaS.Mocker.Stubs.Stubs;

namespace QaaS.Mocker.Servers.Tests;

[TestFixture]
public class GrpcServerTests
{
    [Test]
    public void ResolveServerCredentials_WhenServerIsInsecure_ReturnsInsecureCredentials()
    {
        var credentials = (ServerCredentials)typeof(GrpcServer)
            .GetMethod("ResolveServerCredentials", BindingFlags.NonPublic | BindingFlags.Static)!
            .Invoke(null, [new GrpcServerConfig { IsSecuredSchema = false }, Globals.Logger])!;

        Assert.That(credentials, Is.SameAs(ServerCredentials.Insecure));
    }

    [Test]
    public void ResolveServerCredentials_WhenCertificatePathIsMissing_ThrowsInvalidOperationException()
    {
        var exception = Assert.Throws<TargetInvocationException>(() =>
            typeof(GrpcServer)
                .GetMethod("ResolveServerCredentials", BindingFlags.NonPublic | BindingFlags.Static)!
                .Invoke(null, [new GrpcServerConfig { IsSecuredSchema = true }, Globals.Logger]));

        Assert.That(exception!.InnerException, Is.TypeOf<InvalidOperationException>());
    }

    [Test]
    public void ResolveServerCredentials_WhenCertificateFileDoesNotExist_ThrowsInvalidOperationException()
    {
        var exception = Assert.Throws<TargetInvocationException>(() =>
            typeof(GrpcServer)
                .GetMethod("ResolveServerCredentials", BindingFlags.NonPublic | BindingFlags.Static)!
                .Invoke(null,
                    [new GrpcServerConfig { IsSecuredSchema = true, CertificatePath = "missing-certificate.pfx" }, Globals.Logger]));

        Assert.That(exception!.InnerException, Is.TypeOf<InvalidOperationException>());
    }

    [Test]
    public void ConvertResponseBody_WithTypedResponse_ReturnsInstance()
    {
        var response = InvokeConvertResponseBody<StringValue>(new StringValue { Value = "typed" });

        Assert.That(response.Value, Is.EqualTo("typed"));
    }

    [Test]
    public void ConvertResponseBody_WithSerializedBytes_DeserializesProtobuf()
    {
        var response = InvokeConvertResponseBody<StringValue>(new StringValue { Value = "bytes" }.ToByteArray());

        Assert.That(response.Value, Is.EqualTo("bytes"));
    }

    [Test]
    public void ConvertResponseBody_WithProtobufMessage_ConvertsToRequestedType()
    {
        var response = InvokeConvertResponseBody<StringValue>(new StringValue { Value = "protobuf" });

        Assert.That(response.Value, Is.EqualTo("protobuf"));
    }

    [Test]
    public void ConvertResponseBody_WithNullBody_ThrowsInvalidOperationException()
    {
        var exception = Assert.Throws<TargetInvocationException>(() => InvokeConvertResponseBody<StringValue>(null));

        Assert.That(exception!.InnerException, Is.TypeOf<InvalidOperationException>());
    }

    [Test]
    public void ConvertResponseBody_WithUnsupportedBodyType_ThrowsInvalidOperationException()
    {
        var exception = Assert.Throws<TargetInvocationException>(() => InvokeConvertResponseBody<StringValue>("text"));

        Assert.That(exception!.InnerException, Is.TypeOf<InvalidOperationException>());
    }

    [Test]
    public void DeserializeMessage_WithProtobufBytes_ReturnsParsedMessage()
    {
        var bytes = new StringValue { Value = "hello" }.ToByteArray();
        var parsed = InvokeDeserializeMessage<StringValue>(bytes);

        Assert.That(parsed.Value, Is.EqualTo("hello"));
    }

    [Test]
    public void DeserializeMessage_WithoutParserProperty_ThrowsInvalidOperationException()
    {
        var exception = Assert.Throws<TargetInvocationException>(() => InvokeDeserializeMessage<MissingParserMessage>([]));

        Assert.That(exception!.InnerException, Is.TypeOf<InvalidOperationException>());
    }

    [Test]
    public void DeserializeMessage_WithNullParser_ThrowsInvalidOperationException()
    {
        var exception = Assert.Throws<TargetInvocationException>(() => InvokeDeserializeMessage<NullParserMessage>([]));

        Assert.That(exception!.InnerException, Is.TypeOf<InvalidOperationException>());
    }

    [Test]
    public void DeserializeMessage_WhenParserLacksByteArrayOverload_ThrowsInvalidOperationException()
    {
        var exception = Assert.Throws<TargetInvocationException>(() =>
            InvokeDeserializeMessage<ParserWithoutByteArrayOverloadMessage>([]));

        Assert.That(exception!.InnerException, Is.TypeOf<InvalidOperationException>());
    }

    [Test]
    public void ResolveGrpcServiceName_WithServiceField_ReturnsConfiguredName()
    {
        var name = (string)typeof(GrpcServer)
            .GetMethod("ResolveGrpcServiceName", BindingFlags.NonPublic | BindingFlags.Static)!
            .Invoke(null, [typeof(NamedGrpcService)])!;

        Assert.That(name, Is.EqualTo("tests.NamedGrpcService"));
    }

    [Test]
    public void ResolveGrpcServiceName_WithoutServiceField_FallsBackToFullTypeName()
    {
        var name = (string)typeof(GrpcServer)
            .GetMethod("ResolveGrpcServiceName", BindingFlags.NonPublic | BindingFlags.Static)!
            .Invoke(null, [typeof(MissingServiceNameType)])!;

        Assert.That(name, Is.EqualTo(typeof(MissingServiceNameType).FullName));
    }

    [Test]
    public void ResolvePath_WithRelativePath_UsesCurrentDirectory()
    {
        var resolved = (string)typeof(GrpcServer)
            .GetMethod("ResolvePath", BindingFlags.NonPublic | BindingFlags.Static)!
            .Invoke(null, ["certs/server.pfx"])!;

        Assert.That(resolved, Is.EqualTo(Path.Combine(Environment.CurrentDirectory, "certs/server.pfx")));
    }

    [Test]
    public void ResolvePath_WithAbsolutePath_ReturnsPathUnchanged()
    {
        var absolutePath = Path.Combine(Path.GetTempPath(), "server.pfx");
        var resolved = (string)typeof(GrpcServer)
            .GetMethod("ResolvePath", BindingFlags.NonPublic | BindingFlags.Static)!
            .Invoke(null, [absolutePath])!;

        Assert.That(resolved, Is.EqualTo(absolutePath));
    }

    [Test]
    public void CreateMarshaller_WithNonProtobufType_ThrowsInvalidOperationException()
    {
        var marshaller = InvokeCreateMarshaller<NonProtobufMessage>();

        Assert.Throws<InvalidOperationException>(() => marshaller.Serializer(new NonProtobufMessage()));
    }

    [Test]
    public void CreateMarshaller_WithProtobufType_RoundTripsMessage()
    {
        var marshaller = InvokeCreateMarshaller<StringValue>();

        var bytes = marshaller.Serializer(new StringValue { Value = "payload" });
        var message = marshaller.Deserializer(bytes);

        Assert.That(message.Value, Is.EqualTo("payload"));
    }

    [Test]
    public void Constructor_WithValidServiceConfig_RegistersServiceDefinitionAndPort()
    {
        var server = CreateServer(_ => new StringValue { Value = "ok" }.ToByteArray());
        var grpcCoreServer = (Server)typeof(GrpcServer)
            .GetField("_grpcServer", BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(server)!;

        Assert.Multiple(() =>
        {
            Assert.That(grpcCoreServer.Services.Count, Is.EqualTo(1));
            Assert.That(grpcCoreServer.Ports.Count, Is.EqualTo(1));
            Assert.That(grpcCoreServer.Ports.Single().Port, Is.EqualTo(50051));
        });
    }

    [Test]
    public async Task CreateUnaryHandler_WithSuccessfulProcessing_ReturnsTypedResponse()
    {
        var server = CreateServer(_ => new StringValue { Value = "handled" }.ToByteArray());
        var handler = InvokeCreateUnaryHandler<StringValue, StringValue>(server, nameof(EchoGrpcService), "Echo");

        var response = await handler(new StringValue { Value = "request" }, CreateServerCallContext("ipv4:127.0.0.1:50051"));

        Assert.That(response.Value, Is.EqualTo("handled"));
    }

    [Test]
    public void CreateUnaryHandler_WhenInternalStubFails_RethrowsFatalProcessingException()
    {
        var server = CreateServer(_ => "not-a-protobuf-response", _ => "still-not-a-protobuf-response");
        var handler = InvokeCreateUnaryHandler<StringValue, StringValue>(server, nameof(EchoGrpcService), "Echo");

        Assert.ThrowsAsync<QaaS.Mocker.Servers.Exceptions.FatalInternalErrorException>(async () =>
            await handler(new StringValue { Value = "request" }, CreateServerCallContext(string.Empty)));
    }

    [Test]
    public void Start_WhenInterruptedAfterStartup_StopsBlockingThread()
    {
        var server = CreateServer(GetFreeTcpPort(), _ => new StringValue { Value = "ok" }.ToByteArray());
        var grpcCoreServer = (Server)typeof(GrpcServer)
            .GetField("_grpcServer", BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(server)!;
        Exception? threadException = null;
        var thread = new Thread(() =>
        {
            try
            {
                server.Start();
            }
            catch (ThreadInterruptedException)
            {
            }
            catch (Exception exception)
            {
                threadException = exception;
            }
        });

        thread.IsBackground = true;
        thread.Start();
        Thread.Sleep(300);

        thread.Interrupt();
        Assert.That(thread.Join(TimeSpan.FromSeconds(2)), Is.True);
        grpcCoreServer.ShutdownAsync().GetAwaiter().GetResult();

        Assert.That(threadException, Is.Null);
    }

    [Test]
    public void BuildServiceDefinition_WhenClientTypeIsMissing_ThrowsArgumentException()
    {
        var server = CreateServer(_ => new StringValue { Value = "ok" }.ToByteArray());
        var serviceConfig = new GrpcServiceConfig
        {
            ServiceName = nameof(ServiceWithoutClient),
            ProtoNamespace = typeof(ServiceWithoutClient).Namespace!,
            AssemblyName = typeof(ServiceWithoutClient).Assembly.GetName().Name!,
            Actions =
            [
                new GrpcEndpointActionConfig
                {
                    RpcName = "Echo",
                    TransactionStubName = "EchoStub"
                }
            ]
        };

        var exception = Assert.Throws<TargetInvocationException>(() => InvokeBuildServiceDefinition(server, serviceConfig));

        Assert.That(exception!.InnerException, Is.TypeOf<ArgumentException>());
    }

    [Test]
    public void BuildServiceDefinition_WhenRpcMethodIsMissing_ThrowsArgumentException()
    {
        var server = CreateServer(_ => new StringValue { Value = "ok" }.ToByteArray());
        var serviceConfig = new GrpcServiceConfig
        {
            ServiceName = nameof(EchoGrpcService),
            ProtoNamespace = typeof(EchoGrpcService).Namespace!,
            AssemblyName = typeof(EchoGrpcService).Assembly.GetName().Name!,
            Actions =
            [
                new GrpcEndpointActionConfig
                {
                    RpcName = "MissingRpc",
                    TransactionStubName = "EchoStub"
                }
            ]
        };

        var exception = Assert.Throws<TargetInvocationException>(() => InvokeBuildServiceDefinition(server, serviceConfig));

        Assert.That(exception!.InnerException, Is.TypeOf<ArgumentException>());
    }

    [Test]
    public void ResolveServerCredentials_WithValidCertificate_ReturnsSslServerCredentials()
    {
        var certificatePath = CreateCertificateFile(useRsaCertificate: true);
        try
        {
            var credentials = (ServerCredentials)typeof(GrpcServer)
                .GetMethod("ResolveServerCredentials", BindingFlags.NonPublic | BindingFlags.Static)!
                .Invoke(null,
                    [new GrpcServerConfig
                    {
                        IsSecuredSchema = true,
                        CertificatePath = certificatePath,
                        CertificatePassword = "password"
                    }, Globals.Logger])!;

            Assert.That(credentials, Is.TypeOf<SslServerCredentials>());
        }
        finally
        {
            File.Delete(certificatePath);
        }
    }

    [Test]
    public void ResolveServerCredentials_WithCertificateWithoutRsaKey_ThrowsInvalidOperationException()
    {
        var certificatePath = CreateCertificateFile(useRsaCertificate: false);
        try
        {
            var exception = Assert.Throws<TargetInvocationException>(() =>
                typeof(GrpcServer)
                    .GetMethod("ResolveServerCredentials", BindingFlags.NonPublic | BindingFlags.Static)!
                    .Invoke(null,
                        [new GrpcServerConfig
                        {
                            IsSecuredSchema = true,
                            CertificatePath = certificatePath,
                            CertificatePassword = "password"
                        }, Globals.Logger]));

            Assert.That(exception!.InnerException, Is.TypeOf<InvalidOperationException>());
        }
        finally
        {
            File.Delete(certificatePath);
        }
    }

    [Test]
    public void ConvertResponseBody_WithDifferentProtobufMessage_UsesIMessageSerializationPath()
    {
        var response = InvokeConvertResponseBody<StringValue>(new BytesValue
        {
            Value = ByteString.CopyFromUtf8("payload")
        });

        Assert.That(response, Is.Not.Null);
    }

    [Test]
    public void ResolveGrpcServiceName_WithNullServiceField_FallsBackToFullTypeName()
    {
        var name = (string)typeof(GrpcServer)
            .GetMethod("ResolveGrpcServiceName", BindingFlags.NonPublic | BindingFlags.Static)!
            .Invoke(null, [typeof(NullNamedGrpcService)])!;

        Assert.That(name, Is.EqualTo(typeof(NullNamedGrpcService).FullName));
    }

    [Test]
    public void ResolveGrpcServiceName_WithoutServiceFieldAndFullName_ThrowsInvalidOperationException()
    {
        var genericParameterType = typeof(List<>).GetGenericArguments()[0];

        var exception = Assert.Throws<TargetInvocationException>(() =>
            typeof(GrpcServer)
                .GetMethod("ResolveGrpcServiceName", BindingFlags.NonPublic | BindingFlags.Static)!
                .Invoke(null, [genericParameterType]));

        Assert.That(exception!.InnerException, Is.TypeOf<InvalidOperationException>());
    }

    private static TResponse InvokeConvertResponseBody<TResponse>(object? body) where TResponse : class
    {
        return (TResponse)typeof(GrpcServer)
            .GetMethod("ConvertResponseBody", BindingFlags.NonPublic | BindingFlags.Static)!
            .MakeGenericMethod(typeof(TResponse))
            .Invoke(null, [body])!;
    }

    private static TMessage InvokeDeserializeMessage<TMessage>(byte[] bytes) where TMessage : class
    {
        return (TMessage)typeof(GrpcServer)
            .GetMethod("DeserializeMessage", BindingFlags.NonPublic | BindingFlags.Static)!
            .MakeGenericMethod(typeof(TMessage))
            .Invoke(null, [bytes])!;
    }

    private static Marshaller<TMessage> InvokeCreateMarshaller<TMessage>() where TMessage : class
    {
        return (Marshaller<TMessage>)typeof(GrpcServer)
            .GetMethod("CreateMarshaller", BindingFlags.NonPublic | BindingFlags.Static)!
            .MakeGenericMethod(typeof(TMessage))
            .Invoke(null, null)!;
    }

    private static UnaryServerMethod<TRequest, TResponse> InvokeCreateUnaryHandler<TRequest, TResponse>(
        GrpcServer server,
        string serviceName,
        string rpcName) where TRequest : class where TResponse : class
    {
        return (UnaryServerMethod<TRequest, TResponse>)typeof(GrpcServer)
            .GetMethod("CreateUnaryHandler", BindingFlags.NonPublic | BindingFlags.Instance)!
            .MakeGenericMethod(typeof(TRequest), typeof(TResponse))
            .Invoke(server, [serviceName, rpcName])!;
    }

    private static ServerServiceDefinition InvokeBuildServiceDefinition(GrpcServer server, GrpcServiceConfig serviceConfig)
    {
        return (ServerServiceDefinition)typeof(GrpcServer)
            .GetMethod("BuildServiceDefinition", BindingFlags.NonPublic | BindingFlags.Instance)!
            .Invoke(server, [serviceConfig])!;
    }

    private static GrpcServer CreateServer(
        Func<StringValue, object?> responseFactory,
        Func<StringValue, object?>? internalErrorResponseFactory = null)
    {
        return CreateServer(50051, responseFactory, internalErrorResponseFactory);
    }

    private static GrpcServer CreateServer(
        int port,
        Func<StringValue, object?> responseFactory,
        Func<StringValue, object?>? internalErrorResponseFactory = null)
    {
        return new GrpcServer(
            new GrpcServerConfig
            {
                Port = port,
                IsLocalhost = true,
                Services =
                [
                    new GrpcServiceConfig
                    {
                        ServiceName = nameof(EchoGrpcService),
                        ProtoNamespace = typeof(EchoGrpcService).Namespace!,
                        AssemblyName = typeof(EchoGrpcService).Assembly.GetName().Name!,
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
            },
            Globals.Logger,
            [
                CreateStub("EchoStub", request =>
                {
                    var requestMessage = (StringValue)request.Body!;
                    return new Data<object> { Body = responseFactory(requestMessage) };
                }),
                CreateStub(Constants.DefaultNotFoundTransactionStubLabel, _ => new Data<object>
                {
                    Body = new StringValue { Value = "not-found" }.ToByteArray()
                }),
                CreateStub(Constants.DefaultInternalErrorTransactionStubLabel, request =>
                {
                    var requestMessage = (StringValue)request.Body!;
                    return new Data<object>
                    {
                        Body = internalErrorResponseFactory?.Invoke(requestMessage)
                               ?? new StringValue { Value = "internal-error" }.ToByteArray()
                    };
                })
            ]);
    }

    private static int GetFreeTcpPort()
    {
        using var listener = new System.Net.Sockets.TcpListener(System.Net.IPAddress.Loopback, 0);
        listener.Start();
        return ((System.Net.IPEndPoint)listener.LocalEndpoint).Port;
    }

    private static TransactionStub CreateStub(string name, Func<Data<object>, Data<object>> process)
    {
        return new TransactionStub
        {
            Name = name,
            Processor = new DelegateProcessor(process),
            DataSourceList = ImmutableList<DataSource>.Empty
        };
    }

    private static ServerCallContext CreateServerCallContext(string peer)
    {
        var context = new Mock<ServerCallContext>();
        context.Protected().SetupGet<string>("PeerCore").Returns(peer);
        return context.Object;
    }

    private static string CreateCertificateFile(bool useRsaCertificate)
    {
        X509Certificate2 certificate;
        if (useRsaCertificate)
        {
            using var rsa = RSA.Create(2048);
            var request = new CertificateRequest("CN=localhost", rsa, HashAlgorithmName.SHA256,
                RSASignaturePadding.Pkcs1);
            certificate = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(1));
        }
        else
        {
            using var ecdsa = ECDsa.Create();
            var request = new CertificateRequest("CN=localhost", ecdsa, HashAlgorithmName.SHA256);
            certificate = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(1));
        }

        using (certificate)
        {
            var path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.pfx");
            File.WriteAllBytes(path, certificate.Export(X509ContentType.Pfx, "password"));
            return path;
        }
    }

    private sealed class DelegateProcessor(Func<Data<object>, Data<object>> process) : ITransactionProcessor
    {
        public Context Context { get; set; } = null!;
        public List<ValidationResult>? LoadAndValidateConfiguration(Microsoft.Extensions.Configuration.IConfiguration configuration)
            => [];
        public Data<object> Process(IImmutableList<DataSource> dataSourceList, Data<object> requestData) => process(requestData);
    }

    private sealed class MissingParserMessage;

    private sealed class NullParserMessage
    {
        public static object? Parser => null;
    }

    private sealed class ParserWithoutByteArrayOverloadMessage
    {
        public static object Parser => new ParserWithoutByteArrayOverload();
    }

    private sealed class ParserWithoutByteArrayOverload
    {
        public ParserWithoutByteArrayOverload ParseFrom(string value) => this;
    }

    private sealed class NonProtobufMessage;

    private sealed class NamedGrpcService
    {
        private static readonly string __ServiceName = "tests.NamedGrpcService";
    }

    private sealed class NullNamedGrpcService
    {
        private static readonly string? __ServiceName = null;
    }

    private sealed class MissingServiceNameType;

}

public sealed class EchoGrpcService
{
    private static readonly string __ServiceName = "tests.EchoGrpcService";

    public sealed class EchoGrpcServiceClient
    {
        public StringValue Echo(StringValue request, CallOptions options) => request;
    }
}

public sealed class ServiceWithoutClient
{
    private static readonly string __ServiceName = "tests.ServiceWithoutClient";
}
