using System.Collections.Immutable;
using System.Reflection;
using System.Security.Cryptography.X509Certificates;
using Google.Protobuf;
using Grpc.Core;
using Microsoft.Extensions.Logging;
using QaaS.Framework.SDK.Session.DataObjects;
using QaaS.Mocker.Servers.ConfigurationObjects.GrpcServerConfigs;
using QaaS.Mocker.Servers.ServerStates;
using QaaS.Mocker.Stubs.Stubs;

namespace QaaS.Mocker.Servers.Servers;

public class GrpcServer : IServer
{
    private static readonly MethodInfo AddMethodGenericDefinition = typeof(ServerServiceDefinition.Builder)
        .GetMethods(BindingFlags.Public | BindingFlags.Instance)
        .First(method => method is { Name: "AddMethod", IsGenericMethodDefinition: true } &&
                         method.GetParameters().Length == 2);

    private static readonly MethodInfo CreateGrpcMethodGenericDefinition = typeof(GrpcServer)
        .GetMethod(nameof(CreateGrpcMethod), BindingFlags.NonPublic | BindingFlags.Instance)!;

    private static readonly MethodInfo CreateUnaryHandlerGenericDefinition = typeof(GrpcServer)
        .GetMethod(nameof(CreateUnaryHandler), BindingFlags.NonPublic | BindingFlags.Instance)!;

    private const string GrpcServiceClientSuffix = "Client";

    private readonly ILogger _logger;
    private readonly GrpcServerConfig _configuration;
    private readonly GrpcServerState _grpcServerState;
    private readonly Server _grpcServer;

    public IServerState State { get; init; }

    public GrpcServer(GrpcServerConfig configuration, ILogger logger, IImmutableList<TransactionStub> transactionStubList)
    {
        _logger = logger;
        _configuration = configuration;

        _grpcServerState = new GrpcServerState(logger, transactionStubList,
            configuration.NotFoundTransactionStubName, configuration.InternalErrorTransactionStubName,
            configuration.Services);
        State = _grpcServerState;

        _grpcServer = new Server();

        foreach (var service in configuration.Services)
            _grpcServer.Services.Add(BuildServiceDefinition(service));

        var host = configuration.IsLocalhost ? "127.0.0.1" : "0.0.0.0";
        _grpcServer.Ports.Add(new ServerPort(host, configuration.Port, ResolveServerCredentials(configuration)));
    }

    public void Start()
    {
        _grpcServer.Start();
        _logger.LogInformation("gRPC Server started. Listening on port {Port} (localhost: {IsLocalhost}, tls: {IsTls})",
            _configuration.Port, _configuration.IsLocalhost, _configuration.IsSecuredSchema);
        Thread.Sleep(Timeout.Infinite);
    }

    private ServerServiceDefinition BuildServiceDefinition(GrpcServiceConfig serviceConfig)
    {
        var serviceDefinitionBuilder = ServerServiceDefinition.CreateBuilder();

        var assembly = Assembly.Load(serviceConfig.AssemblyName);
        var serviceType = assembly.GetType($"{serviceConfig.ProtoNamespace}.{serviceConfig.ServiceName}", throwOnError: true)!;
        var grpcServiceName = ResolveGrpcServiceName(serviceType);
        var clientType = serviceType.GetNestedType($"{serviceConfig.ServiceName}{GrpcServiceClientSuffix}",
                             BindingFlags.Public | BindingFlags.NonPublic) ??
                         throw new ArgumentException(
                             $"Could not resolve gRPC client type for service '{serviceConfig.ServiceName}' in '{serviceConfig.AssemblyName}'.");

        foreach (var action in serviceConfig.Actions)
        {
            var clientMethod = clientType
                                   .GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                                   .FirstOrDefault(method =>
                                       method.Name == action.RpcName
                                       && method.GetParameters().Length == 2
                                       && method.GetParameters()[1].ParameterType == typeof(CallOptions))
                               ?? throw new ArgumentException(
                                   $"Could not find RPC method '{action.RpcName}' on service '{serviceConfig.ServiceName}'.");

            var requestType = clientMethod.GetParameters()[0].ParameterType;
            var responseType = clientMethod.ReturnType;

            AddServiceMethod(serviceDefinitionBuilder, requestType, responseType, grpcServiceName, serviceConfig.ServiceName,
                action.RpcName);
            _logger.LogInformation("Registered gRPC action Service '{ServiceName}' Rpc '{RpcName}'",
                serviceConfig.ServiceName, action.RpcName);
        }

        return serviceDefinitionBuilder.Build();
    }

    private void AddServiceMethod(
        ServerServiceDefinition.Builder serviceDefinitionBuilder,
        Type requestType,
        Type responseType,
        string grpcServiceName,
        string serviceName,
        string rpcName)
    {
        var method = CreateGrpcMethodGenericDefinition.MakeGenericMethod(requestType, responseType)
            .Invoke(this, [grpcServiceName, rpcName])!;
        var handler = CreateUnaryHandlerGenericDefinition.MakeGenericMethod(requestType, responseType)
            .Invoke(this, [serviceName, rpcName])!;

        AddMethodGenericDefinition.MakeGenericMethod(requestType, responseType)
            .Invoke(serviceDefinitionBuilder, [method, handler]);
    }

    private Method<TRequest, TResponse> CreateGrpcMethod<TRequest, TResponse>(string grpcServiceName, string rpcName)
        where TRequest : class where TResponse : class
    {
        return new Method<TRequest, TResponse>(MethodType.Unary, grpcServiceName, rpcName,
            CreateMarshaller<TRequest>(), CreateMarshaller<TResponse>());
    }

    private UnaryServerMethod<TRequest, TResponse> CreateUnaryHandler<TRequest, TResponse>(
        string serviceName,
        string rpcName) where TRequest : class where TResponse : class
    {
        return (request, _) =>
        {
            var responseData = _grpcServerState.Process(serviceName, rpcName, new Data<object> { Body = request });
            return Task.FromResult(ConvertResponseBody<TResponse>(responseData.Body));
        };
    }

    private static Marshaller<TMessage> CreateMarshaller<TMessage>() where TMessage : class
    {
        return Marshallers.Create(
            message => message is IMessage protobufMessage
                ? protobufMessage.ToByteArray()
                : throw new InvalidOperationException(
                    $"Message type '{typeof(TMessage)}' must implement Google.Protobuf.IMessage"),
            DeserializeMessage<TMessage>);
    }

    private static TMessage DeserializeMessage<TMessage>(byte[] bytes) where TMessage : class
    {
        var parserProperty = typeof(TMessage).GetProperty("Parser", BindingFlags.Public | BindingFlags.Static) ??
                             throw new InvalidOperationException(
                                 $"Message type '{typeof(TMessage)}' does not expose static Parser property.");
        var parser = parserProperty.GetValue(null) ??
                     throw new InvalidOperationException(
                         $"Message type '{typeof(TMessage)}' parser could not be resolved.");
        var parseFromMethod = parser.GetType().GetMethod("ParseFrom", [typeof(byte[])]) ??
                              throw new InvalidOperationException(
                                  $"Parser of '{typeof(TMessage)}' does not support ParseFrom(byte[]).");

        return (TMessage)parseFromMethod.Invoke(parser, [bytes])!;
    }

    private static TResponse ConvertResponseBody<TResponse>(object? body) where TResponse : class
    {
        return body switch
        {
            null => throw new InvalidOperationException("gRPC response body cannot be null"),
            TResponse typedBody => typedBody,
            byte[] bytes => DeserializeMessage<TResponse>(bytes),
            IMessage protobufMessage => DeserializeMessage<TResponse>(protobufMessage.ToByteArray()),
            _ => throw new InvalidOperationException(
                $"Unsupported gRPC response body type '{body.GetType()}'. Expected '{typeof(TResponse)}' or byte[].")
        };
    }

    private static string ResolveGrpcServiceName(Type serviceType)
    {
        return serviceType.GetField("__ServiceName", BindingFlags.NonPublic | BindingFlags.Static)
                   ?.GetValue(null)?.ToString()
               ?? serviceType.FullName
               ?? throw new InvalidOperationException("Could not resolve grpc service name.");
    }

    private static ServerCredentials ResolveServerCredentials(GrpcServerConfig configuration)
    {
        if (!configuration.IsSecuredSchema)
            return ServerCredentials.Insecure;

        if (string.IsNullOrWhiteSpace(configuration.CertificatePath))
            throw new InvalidOperationException(
                "CertificatePath is required when gRPC IsSecuredSchema is true.");

        var certificatePath = ResolvePath(configuration.CertificatePath);
        var pfxCertificate = new X509Certificate2(certificatePath, configuration.CertificatePassword,
            X509KeyStorageFlags.Exportable);
        var certificate = pfxCertificate.ExportCertificatePem();
        var privateKey = pfxCertificate.GetRSAPrivateKey()?.ExportPkcs8PrivateKeyPem()
                         ?? throw new InvalidOperationException(
                             "Configured gRPC certificate does not contain an exportable RSA private key.");

        return new SslServerCredentials([new KeyCertificatePair(certificate, privateKey)]);
    }

    private static string ResolvePath(string path)
    {
        return Path.IsPathRooted(path)
            ? path
            : Path.Combine(Environment.CurrentDirectory, path);
    }
}
