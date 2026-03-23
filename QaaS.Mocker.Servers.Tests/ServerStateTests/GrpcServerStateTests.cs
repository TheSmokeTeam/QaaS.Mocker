using System.Collections.Immutable;
using System.ComponentModel.DataAnnotations;
using System.Text;
using System.Reflection;
using Microsoft.Extensions.Configuration;
using NUnit.Framework;
using QaaS.Framework.SDK.ContextObjects;
using QaaS.Framework.SDK.DataSourceObjects;
using QaaS.Framework.SDK.Hooks.Processor;
using QaaS.Framework.SDK.Session.DataObjects;
using QaaS.Mocker.Servers.Actions;
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
    public void Process_WithUnknownService_UsesNotFoundStub()
    {
        var state = CreateState(
            ("MainStub", _ => CreateResponse("main")),
            ("NotFoundStub", _ => CreateResponse("not-found")),
            ("InternalStub", _ => CreateResponse("internal")));

        var response = state.Process("MissingService", "Echo", CreateRequestData());

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
    public void Process_WhenPrimaryAndInternalErrorStubsThrow_ThrowsFatalInternalErrorException()
    {
        var state = CreateState(
            ("MainStub", _ => throw new InvalidOperationException("boom")),
            ("NotFoundStub", _ => CreateResponse("not-found")),
            ("InternalStub", _ => throw new InvalidOperationException("internal-boom")));

        Assert.Throws<FatalInternalErrorException>(() =>
            state.Process("EchoService", "Echo", CreateRequestData()));
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
    public void ChangeActionStub_WhenStubDoesNotExist_Throws()
    {
        var state = CreateState(
            ("MainStub", _ => CreateResponse("main")),
            ("NotFoundStub", _ => CreateResponse("not-found")),
            ("InternalStub", _ => CreateResponse("internal")));

        Assert.Throws<StubNotLoadedException>(() => state.ChangeActionStub("EchoAction", "MissingStub"));
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

    [Test]
    public void HasAction_ReturnsExpectedValue()
    {
        var state = CreateState(
            ("MainStub", _ => CreateResponse("main")),
            ("NotFoundStub", _ => CreateResponse("not-found")),
            ("InternalStub", _ => CreateResponse("internal")));

        Assert.Multiple(() =>
        {
            Assert.That(state.HasAction("EchoAction"), Is.True);
            Assert.That(state.HasAction("missing"), Is.False);
        });
    }

    [Test]
    public void ResolveActionName_WithNullStoredActionName_FallsBackToNotFoundAction()
    {
        var state = CreateState(
            ("MainStub", _ => CreateResponse("main")),
            ("NotFoundStub", _ => CreateResponse("not-found")),
            ("InternalStub", _ => CreateResponse("internal")));
        var rpcToActionField = typeof(GrpcServerState)
            .GetField("_rpcToAction", BindingFlags.Instance | BindingFlags.NonPublic)!;
        var rpcToAction = (IDictionary<string, ActionToTransactionStub>)rpcToActionField.GetValue(state)!;
        rpcToAction["EchoService/Echo"].ActionName = null;
        var resolveActionNameMethod = typeof(GrpcServerState)
            .GetMethod("ResolveActionName", BindingFlags.Instance | BindingFlags.NonPublic)!;

        var actionName = (string)resolveActionNameMethod.Invoke(state, ["EchoService", "Echo"])!;

        Assert.That(actionName, Is.EqualTo("NotFoundTransactionStub"));
    }

    [Test]
    public void Constructor_WithUnnamedAction_UsesServiceAndRpcFallbackActionName()
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
                        Name = null,
                        RpcName = "Echo",
                        TransactionStubName = "MainStub"
                    }
                ]
            }
        ];

        var state = new GrpcServerState(
            Globals.Logger,
            ImmutableList.Create(
                CreateStub("MainStub", _ => CreateResponse("main")),
                CreateStub("NotFoundStub", _ => CreateResponse("not-found")),
                CreateStub("InternalStub", _ => CreateResponse("internal"))),
            "NotFoundStub",
            "InternalStub",
            services);

        Assert.Multiple(() =>
        {
            Assert.That(state.HasAction("EchoService.Echo"), Is.True);
            Assert.That(state.HasAction("EchoAction"), Is.False);
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
