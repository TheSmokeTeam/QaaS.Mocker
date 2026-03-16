using System.Collections.Immutable;
using Microsoft.Extensions.Logging;
using QaaS.Framework.SDK.ConfigurationObjects;
using QaaS.Framework.SDK.ContextObjects;
using QaaS.Framework.SDK.DataSourceObjects;
using QaaS.Framework.SDK.Hooks.Processor;
using QaaS.Mocker.Stubs.ConfigurationObjects;
using QaaS.Mocker.Stubs.Processors;
using QaaS.Mocker.Stubs.Stubs;

namespace QaaS.Mocker.Stubs;

public class StubFactory(Context context, TransactionStubConfig[] stubs, 
    IList<KeyValuePair<string, ITransactionProcessor>> transactionProcessors) 
{
    public IImmutableList<TransactionStub> Build(IImmutableList<DataSource> dataSourceList)
    {
        var transactionStubs = new List<TransactionStub>();

        foreach (var transactionStubConfig in stubs)
        {
            var transactionProcessor = 
                transactionProcessors.FirstOrDefault(pair => pair.Key==transactionStubConfig.Name!).Value ??
                throw new ArgumentException($"Transaction Stub {transactionStubConfig.Name}'s provided transaction" +
                                            $" processor {transactionStubConfig.Processor} was not found in provided " +
                                            $"processors.");
        
            context.Logger.LogInformation(
                "Building transaction stub '{TransactionStubName}' with processor '{TransactionStubProcessor}'",
                transactionStubConfig.Name, transactionStubConfig.Processor);
            
            var transactionStubDataSources = transactionStubConfig.DataSourceNames.Select(dataSourceName =>
                    dataSourceList.FirstOrDefault(dataSource => dataSource.Name == dataSourceName) ??
                    throw new ArgumentException($"Could not find data source {dataSourceName} " +
                                                $"to pass to transaction stub {transactionStubConfig.Name}"))
                .ToImmutableList();
            
            
            context.Logger.LogDebug(
                "Transaction stub '{TransactionStubName}' is configured with {DataSourcesPassedCount} data source(s), request deserializer '{RequestDeserializer}', and response serializer '{ResponseSerializer}'",
                transactionStubConfig.Name,
                transactionStubDataSources.Count,
                transactionStubConfig.RequestBodyDeserialization?.Deserializer?.ToString() ?? "<none>",
                transactionStubConfig.ResponseBodySerialization?.Serializer?.ToString() ?? "<none>");
            
            transactionStubs.Add(new TransactionStub
            {
                Name = transactionStubConfig.Name!,
                Processor = transactionProcessor,
                DataSourceList = transactionStubDataSources,
                RequestBodyDeserializer = Framework.Serialization.DeserializerFactory.BuildDeserializer(transactionStubConfig.RequestBodyDeserialization?.Deserializer),
                RequestBodyDeserializerSpecificType = transactionStubConfig.RequestBodyDeserialization?.SpecificType?.GetConfiguredType(),
                ResponseBodySerializer = Framework.Serialization.SerializerFactory.BuildSerializer(transactionStubConfig.ResponseBodySerialization?.Serializer)
            });
        }

        transactionStubs.Add(new TransactionStub
        {
            Name = Constants.DefaultNotFoundTransactionStubLabel,
            Processor = new StatusCodeTransactionProcessor(Constants.DefaultNotFoundTransactionStubStatusCode),
        });
        
        transactionStubs.Add(new TransactionStub
        {
            Name = Constants.DefaultInternalErrorTransactionStubLabel,
            Processor = new StatusCodeTransactionProcessor(Constants.DefaultInternalErrorTransactionStubStatusCode),        
        });

        context.Logger.LogInformation(
            "Built {TransactionStubCount} transaction stub(s) including default not-found and internal-error stubs",
            transactionStubs.Count);
        
        return transactionStubs.ToImmutableList();
    }
}
