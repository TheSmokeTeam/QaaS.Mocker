using System.Collections.Immutable;
using System.ComponentModel.DataAnnotations;
using Microsoft.Extensions.Configuration;
using QaaS.Framework.SDK.ContextObjects;
using QaaS.Framework.SDK.DataSourceObjects;
using QaaS.Framework.SDK.Hooks.Processor;
using QaaS.Framework.SDK.Session.DataObjects;
using QaaS.Framework.SDK.Session.MetaDataObjects;

namespace QaaS.Mocker.Stubs.Processors;

internal sealed class StatusCodeTransactionProcessor(int statusCode) : ITransactionProcessor
{
    public Context Context { get; set; } = null!;

    public List<ValidationResult>? LoadAndValidateConfiguration(IConfiguration configuration) => [];

    public Data<object> Process(IImmutableList<DataSource> dataSourceList, Data<object> requestData)
    {
        return new Data<object>
        {
            Body = Array.Empty<byte>(),
            MetaData = new MetaData
            {
                Http = new Http
                {
                    StatusCode = statusCode
                }
            }
        };
    }
}
