using System.Collections.Immutable;
using Google.Protobuf;
using QaaS.Framework.SDK.DataSourceObjects;
using QaaS.Framework.SDK.Hooks.Processor;
using QaaS.Framework.SDK.Session.DataObjects;
using QaaS.Mocker.Example.Grpc;

namespace QaaS.Mocker.Example.Processors;

/// <summary>
/// Echoes sample gRPC request messages back to the caller.
/// </summary>
public sealed class GrpcEchoProcessor : BaseTransactionProcessor<NoConfiguration>
{
    /// <summary>
    /// Validates that the request body is an <see cref="EchoRequest"/> and returns a serialized response.
    /// </summary>
    public override Data<object> Process(IImmutableList<DataSource> dataSourceList, Data<object> requestData)
    {
        if (requestData.Body is not EchoRequest request)
            throw new ArgumentException("GrpcEchoProcessor expects EchoRequest request body.");

        return new Data<object>
        {
            Body = new EchoResponse
            {
                Message = request.Message,
                Code = 200
            }.ToByteArray()
        };
    }
}
