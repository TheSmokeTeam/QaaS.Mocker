using System.Collections.Immutable;
using Microsoft.Extensions.Configuration;
using NUnit.Framework;
using QaaS.Framework.SDK.ContextObjects;
using QaaS.Framework.SDK.DataSourceObjects;
using QaaS.Framework.SDK.Hooks.Processor;
using QaaS.Framework.SDK.Session.DataObjects;
using QaaS.Mocker.Options;

namespace QaaS.Mocker.Tests.ExecutionTests;

[TestFixture]
public class ProcessorHooksIntegrationTests
{
    [Test]
    public void Build_WithProcessorFromLoadedAssemblies_Succeeds()
    {
        var context = new InternalContext
        {
            Logger = Globals.Logger,
            RootConfiguration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Stubs:0:Name"] = "ExampleStub",
                    ["Stubs:0:Processor"] = nameof(TestProcessor),
                    ["Server:Type"] = "Http",
                    ["Server:Http:Port"] = "18080",
                    ["Server:Http:IsLocalhost"] = "true",
                    ["Server:Http:Endpoints:0:Path"] = "/health",
                    ["Server:Http:Endpoints:0:Actions:0:Name"] = "HealthAction",
                    ["Server:Http:Endpoints:0:Actions:0:Method"] = "Get",
                    ["Server:Http:Endpoints:0:Actions:0:TransactionStubName"] = "ExampleStub"
                })
                .Build()
        };

        var builder = new ExecutionBuilder(context, ExecutionMode.Template, runLocally: false, templateOutputFolder: null);

        Assert.DoesNotThrow(() => builder.Build());
    }

    private sealed class TestProcessor : BaseTransactionProcessor<TestProcessorConfig>
    {
        public override Data<object> Process(IImmutableList<DataSource> dataSourceList, Data<object> requestData)
            => new() { Body = "ok"u8.ToArray() };
    }

    private sealed class TestProcessorConfig;
}

