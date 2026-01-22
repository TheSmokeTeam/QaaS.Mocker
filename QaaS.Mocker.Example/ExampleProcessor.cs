using System.Collections.Immutable;
using System.ComponentModel.DataAnnotations;
using System.Text;
using Microsoft.Extensions.Configuration;
using QaaS.Framework.SDK.ContextObjects;
using QaaS.Framework.SDK.DataSourceObjects;
using QaaS.Framework.SDK.Hooks.Processor;
using QaaS.Framework.SDK.Session.DataObjects;
using QaaS.Framework.SDK.Session.MetaDataObjects;

namespace QaaS.Mocker.Example;

public class ExampleProcessor : ITransactionProcessor
{
    public List<ValidationResult>? LoadAndValidateConfiguration(IConfiguration configuration) => [];

    public Context Context { get; set; }
    public Data<object> Process(IImmutableList<DataSource> dataSourceList, Data<object> requestData)
    {
        return new Data<object> {
            Body = Encoding.UTF8.GetBytes("Hello world! This is an example :)"),
            MetaData = new MetaData { Http = new Http { StatusCode = 200 } } 
        };
    }
}