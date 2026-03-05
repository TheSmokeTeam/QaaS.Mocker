using System.Collections.Immutable;
using System.ComponentModel.DataAnnotations;
using System.Text;
using Microsoft.Extensions.Configuration;
using NUnit.Framework;
using QaaS.Framework.SDK.ContextObjects;
using QaaS.Framework.SDK.DataSourceObjects;
using QaaS.Framework.SDK.Hooks.Processor;
using QaaS.Framework.SDK.Session.DataObjects;
using QaaS.Mocker.Servers.ConfigurationObjects.GrpcServerConfigs;
using QaaS.Mocker.Servers.Exceptions;
using QaaS.Mocker.Servers.ServerStates;
using QaaS.Mocker.Stubs.Stubs;

namespace QaaS.Mocker.Servers.Tests.ServerStateTests;

[TestFixture]
public class GrpcServerStateTests
{
    [Test]
    public void Process_WithKnownRpc_UsesMappedStub()
    {
        var state = CreateState(
            ("MainStub", _ => CreateResponse("main")),
            ("NotFoundStub", _ => CreateResponse("not-found")),
            ("InternalStub", _ => CreateResponse("internal")));

        var response = state.Process("EchoService", "Echo", CreateRequestData());

        Assert.That(Encoding.UTF8.GetString((byte[])response.Body!), Is.EqualTo("main"));
    }

    [Test]
    public void Process_WithUnknownRpc_UsesNotFoundStub()
    {
        var state = CreateState(
            ("MainStub", _ => CreateResponse("main")),
            ("NotFoundStub", _ => CreateResponse("not-found")),
            ("InternalStub", _ => CreateResponse("internal")));

        var response = state.Process("EchoService", "Missing", CreateRequestData());

        Assert.That(Encoding.UTF8.GetString((byte[])response.Body!), Is.EqualTo("not-found"));
    }

    [Test]
    public void Process_WhenPrimaryStubThrows_UsesInternalErrorStub()
    {
        var state = CreateState(
            ("MainStub", _ => throw new InvalidOperationException("boom")),
            ("NotFoundStub", _ => CreateResponse("not-found")),
            ("InternalStub", _ => CreateResponse("internal")));

        var response = state.Process("EchoService", "Echo", CreateRequestData());

        Assert.That(Encoding.UTF8.GetString((byte[])response.Body!), Is.EqualTo("internal"));
    }

    [Test]
    public void ChangeActionStub_UsesCaseInsensitiveActionName()
    {
        var state = CreateState(
            ("MainStub", _ => CreateResponse("main")),
            ("AltStub", _ => CreateResponse("alt")),
            ("NotFoundStub", _ => CreateResponse("not-found")),
            ("InternalStub", _ => CreateResponse("internal")));

        state.ChangeActionStub("echoaction", "AltStub");
        var response = state.Process("EchoService", "Echo", CreateRequestData());

        Assert.That(Encoding.UTF8.GetString((byte[])response.Body!), Is.EqualTo("alt"));
    }

    [Test]
    public void ChangeActionStub_WhenActionDoesNotExist_Throws()
    {
        var state = CreateState(
            ("MainStub", _ => CreateResponse("main")),
            ("NotFoundStub", _ => CreateResponse("not-found")),
            ("InternalStub", _ => CreateResponse("internal")));

        Assert.Throws<ActionDoesNotExistException>(() => state.ChangeActionStub("missing", "MainStub"));
    }

    [Test]
    public void TriggerAction_ThrowsNotImplementedException()
    {
        var state = CreateState(
            ("MainStub", _ => CreateResponse("main")),
            ("NotFoundStub", _ => CreateResponse("not-found")),
            ("InternalStub", _ => CreateResponse("internal")));

        Assert.Throws<NotImplementedException>(() => state.TriggerAction("EchoAction", 100));
    }

    [Test]
    public void Process_WithCacheEnabled_StoresInputAndOutput()
    {
        var state = CreateState(
            ("MainStub", _ => CreateResponse("main")),
            ("NotFoundStub", _ => CreateResponse("not-found")),
            ("InternalStub", _ => CreateResponse("internal")));

        var cache = state.GetCache();
        cache.EnableStorage = true;

        _ = state.Process("EchoService", "Echo", CreateRequestData());

        Assert.Multiple(() =>
        {
            Assert.That(cache.RetrieveFirstOrDefaultStringInput(), Is.Not.Null);
            Assert.That(cache.RetrieveFirstOrDefaultStringOutput(), Is.Not.Null);
        });
    }

    private static GrpcServerState CreateState(params (string Name, Func<Data<object>, Data<object>> Processor)[] stubs)
    {
        GrpcServiceConfig[] services =
        [
            new()
            {
                ServiceName = "EchoService",
                ProtoNamespace = "Tests",
                AssemblyName = "Tests",
                Actions =
                [
                    new GrpcEndpointActionConfig
                    {
                        Name = "EchoAction",
                        RpcName = "Echo",
                        TransactionStubName = "MainStub"
                    }
                ]
            }
        ];

        return new GrpcServerState(
            Globals.Logger,
            stubs.Select(tuple => CreateStub(tuple.Name, tuple.Processor)).ToImmutableList(),
            "NotFoundStub",
            "InternalStub",
            services);
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

    private static Data<object> CreateResponse(string payload)
    {
        return new Data<object>
        {
            Body = Encoding.UTF8.GetBytes(payload)
        };
    }

    private static Data<object> CreateRequestData()
    {
        return new Data<object>
        {
            Body = Encoding.UTF8.GetBytes("request")
        };
    }

    private sealed class DelegateProcessor(Func<Data<object>, Data<object>> process) : ITransactionProcessor
    {
        public Context Context { get; set; } = null!;
        public List<ValidationResult>? LoadAndValidateConfiguration(IConfiguration configuration) => [];
        public Data<object> Process(IImmutableList<DataSource> dataSourceList, Data<object> requestData) => process(requestData);
    }
}
