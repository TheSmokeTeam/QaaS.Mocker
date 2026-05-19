using NUnit.Framework;
using QaaS.Framework.SDK.DataSourceObjects;
using QaaS.Mocker.Controller.ConfigurationObjects;
using QaaS.Mocker.Servers.ConfigurationObjects;
using QaaS.Mocker.Stubs.ConfigurationObjects;

namespace QaaS.Mocker.Tests.BuilderTests;

public class CloneSmokeTest
{
    [Test]
    public void Clone_AllBuilders_ProducesIndependentDeepCopies()
    {
        var stubBuilder = new TransactionStubBuilder()
            .Named("stub1")
            .HookNamed("MyProcessor")
            .AddDataSourceName("ds1");
        var clonedStub = stubBuilder.Clone();

        var dataSourceBuilder = new DataSourceBuilder().Named("ds1").HookNamed("MyGenerator");

        var executionBuilder = new ExecutionBuilder()
            .AddDataSource(dataSourceBuilder)
            .AddStub(stubBuilder)
            .WithServer(new ServerConfig())
            .WithController(new ControllerConfig());
        var clonedExecution = executionBuilder.Clone();

        Assert.Multiple(() =>
        {
            Assert.That(clonedStub, Is.Not.SameAs(stubBuilder));
            Assert.That(clonedStub.DataSourceNames, Is.Not.SameAs(stubBuilder.DataSourceNames));

            Assert.That(clonedExecution, Is.Not.SameAs(executionBuilder));
            Assert.That(clonedExecution.Stubs, Is.Not.SameAs(executionBuilder.Stubs));
            Assert.That(clonedExecution.Stubs[0], Is.Not.SameAs(executionBuilder.Stubs[0]));
            Assert.That(clonedExecution.Server, Is.Not.SameAs(executionBuilder.Server));
            Assert.That(clonedExecution.Controller, Is.Not.SameAs(executionBuilder.Controller));
            Assert.That(clonedExecution.DataSources![0], Is.Not.SameAs(executionBuilder.DataSources![0]));

            clonedStub.DataSourceNames[0] = "mutated";
            Assert.That(stubBuilder.DataSourceNames[0], Is.EqualTo("ds1"));
        });
    }
}
