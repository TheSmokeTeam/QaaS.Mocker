using System.Collections.Immutable;
using QaaS.Framework.SDK.DataSourceObjects;
using QaaS.Framework.SDK.Hooks.Processor;
using QaaS.Framework.SDK.Session.DataObjects;
using QaaS.Framework.SDK.Session.MetaDataObjects;

namespace QaaS.Mocker.Example.Processors;

public sealed class ExampleProcessor : BaseTransactionProcessor<NoConfiguration>
{
    public override Data<object> Process(IImmutableList<DataSource> dataSourceList, Data<object> requestData)
    {
        return new Data<object>
        {
            Body = "healthy"u8.ToArray(),
            MetaData = new MetaData
            {
                Http = new Http
                {
                    StatusCode = 200,
                    Headers = new Dictionary<string, string>
                    {
                        ["Content-Type"] = "text/plain; charset=utf-8"
                    }
                }
            }
        };
    }
}

public sealed record NoConfiguration;
