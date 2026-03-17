using System.Collections.Immutable;
using System.Text;
using Google.Protobuf;
using QaaS.Framework.SDK.DataSourceObjects;
using QaaS.Framework.SDK.Hooks.Processor;
using QaaS.Framework.SDK.Session.DataObjects;
using QaaS.Framework.SDK.Session.MetaDataObjects;
using QaaS.Mocker.Example.Grpc;

namespace QaaS.Mocker.Example.Processors;

public sealed class AlternateHealthProcessor : BaseTransactionProcessor<NoConfiguration>
{
    public override Data<object> Process(IImmutableList<DataSource> dataSourceList, Data<object> requestData)
    {
        return new Data<object>
        {
            Body = "degraded"u8.ToArray(),
            MetaData = new MetaData
            {
                Http = new Http
                {
                    StatusCode = 503,
                    Headers = new Dictionary<string, string>
                    {
                        ["Content-Type"] = "text/plain; charset=utf-8"
                    }
                }
            }
        };
    }
}

public sealed class AlternateGrpcEchoProcessor : BaseTransactionProcessor<NoConfiguration>
{
    public override Data<object> Process(IImmutableList<DataSource> dataSourceList, Data<object> requestData)
    {
        if (requestData.Body is not EchoRequest request)
            throw new ArgumentException("AlternateGrpcEchoProcessor expects EchoRequest request body.");

        return new Data<object>
        {
            Body = new EchoResponse
            {
                Message = $"override:{request.Message}",
                Code = 503
            }.ToByteArray()
        };
    }
}

public sealed class SocketPassthroughProcessor : BaseTransactionProcessor<NoConfiguration>
{
    public override Data<object> Process(IImmutableList<DataSource> dataSourceList, Data<object> requestData)
    {
        return new Data<object>
        {
            Body = requestData.Body switch
            {
                byte[] bytes => bytes.ToArray(),
                string text => Encoding.UTF8.GetBytes(text),
                _ => throw new ArgumentException("SocketPassthroughProcessor expects byte[] or string request body.")
            }
        };
    }
}

public sealed class SocketPrefixProcessor : BaseTransactionProcessor<NoConfiguration>
{
    public override Data<object> Process(IImmutableList<DataSource> dataSourceList, Data<object> requestData)
    {
        var text = requestData.Body switch
        {
            byte[] bytes => Encoding.UTF8.GetString(bytes),
            string value => value,
            _ => throw new ArgumentException("SocketPrefixProcessor expects byte[] or string request body.")
        };

        return new Data<object>
        {
            Body = Encoding.UTF8.GetBytes($"override:{text}")
        };
    }
}
