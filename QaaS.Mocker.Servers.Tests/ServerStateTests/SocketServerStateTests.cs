using System.Collections.Immutable;
using System.ComponentModel.DataAnnotations;
using System.Net.Sockets;
using System.Text;
using Microsoft.Extensions.Configuration;
using NUnit.Framework;
using QaaS.Framework.SDK.ContextObjects;
using QaaS.Framework.SDK.DataSourceObjects;
using QaaS.Framework.SDK.Hooks.Processor;
using QaaS.Framework.SDK.Session.DataObjects;
using QaaS.Framework.SDK.Session.MetaDataObjects;
using QaaS.Mocker.Servers.ConfigurationObjects.SocketServerConfigs;
using QaaS.Mocker.Servers.Exceptions;
using QaaS.Mocker.Servers.ServerStates;
using QaaS.Mocker.Stubs.Stubs;

namespace QaaS.Mocker.Servers.Tests.ServerStateTests;

[TestFixture]
public class SocketServerStateTests
{
    [Test]
    public void Constructor_WithCollectAndBroadcastEndpoints_SetsBothInputOutputState()
    {
        var state = CreateState(
            [
                BuildEndpoint(7001, "CollectAction", SocketMethod.Collect),
                BuildEndpoint(7002, "BroadcastAction", SocketMethod.Broadcast, dataSourceName: "ds1")
            ]);

        Assert.That(state.InputOutputState, Is.EqualTo(QaaS.Framework.SDK.ConfigurationObjects.InputOutputState.BothInputOutput));
    }

    [Test]
    public void Process_WithKnownCollectPort_StoresInputWhenCacheEnabled()
    {
        var state = CreateState([BuildEndpoint(7001, "CollectAction", SocketMethod.Collect)]);
        var cache = state.GetCache();
        cache.EnableStorage = true;

        _ = state.Process(7001, [CreateRequest("payload")]).ToList();

        Assert.Multiple(() =>
        {
            Assert.That(cache.RetrieveFirstOrDefaultStringInput(), Is.Not.Null);
            Assert.That(cache.RetrieveFirstOrDefaultStringOutput(), Is.Null);
        });
    }

    [Test]
    public void Process_WithUnknownPort_DoesNotThrowAndReturnsOriginalData()
    {
        var state = CreateState([BuildEndpoint(7001, "CollectAction", SocketMethod.Collect)]);
        var input = CreateRequest("payload");

        var result = state.Process(9999, [input]).Single();

        Assert.That(Encoding.UTF8.GetString((byte[])result.Body!), Is.EqualTo("payload"));
    }

    [Test]
    public async Task TriggerAction_WithExistingAction_EnablesTemporarily()
    {
        var state = CreateState([BuildEndpoint(7001, "CollectAction", SocketMethod.Collect)]);

        state.TriggerAction("CollectAction", 80);

        var becameEnabled = false;
        for (var i = 0; i < 20; i++)
        {
            if (state.IsEndpointPortActionEnabled(7001))
            {
                becameEnabled = true;
                break;
            }

            await Task.Delay(5);
        }

        var becameDisabled = false;
        for (var i = 0; i < 40; i++)
        {
            if (!state.IsEndpointPortActionEnabled(7001))
            {
                becameDisabled = true;
                break;
            }

            await Task.Delay(5);
        }

        Assert.Multiple(() =>
        {
            Assert.That(becameEnabled, Is.True);
            Assert.That(becameDisabled, Is.True);
        });
    }

    [Test]
    public void TriggerAction_WithUnknownAction_Throws()
    {
        var state = CreateState([BuildEndpoint(7001, "CollectAction", SocketMethod.Collect)]);

        Assert.Throws<ActionDoesNotExistException>(() => state.TriggerAction("MissingAction", 100));
    }

    [Test]
    public void ChangeActionStub_WhenActionExists_ChangesRoutedStub()
    {
        var state = CreateState(
            [BuildEndpoint(7001, "CollectAction", SocketMethod.Collect, transactionStubName: "MainStub")],
            ("MainStub", _ => CreateRequest("main")),
            ("AltStub", _ => CreateRequest("alt")));

        state.ChangeActionStub("CollectAction", "AltStub");
        var result = state.Process(7001, [CreateRequest("input")]).Single();

        Assert.That(Encoding.UTF8.GetString((byte[])result.Body!), Is.EqualTo("alt"));
    }

    [Test]
    public void ChangeActionStub_WhenActionNameDiffersByCase_ChangesRoutedStub()
    {
        var state = CreateState(
            [BuildEndpoint(7001, "CollectAction", SocketMethod.Collect, transactionStubName: "MainStub")],
            ("MainStub", _ => CreateRequest("main")),
            ("AltStub", _ => CreateRequest("alt")));

        state.ChangeActionStub("collectaction", "AltStub");
        var result = state.Process(7001, [CreateRequest("input")]).Single();

        Assert.That(Encoding.UTF8.GetString((byte[])result.Body!), Is.EqualTo("alt"));
    }

    [Test]
    public void Constructor_WithCaseInsensitiveStubName_ResolvesStub()
    {
        var endpoint = BuildEndpoint(7001, "CollectAction", SocketMethod.Collect, transactionStubName: "MAINSTUB");
        var state = CreateState([endpoint], ("mainstub", _ => CreateRequest("processed")));

        var result = state.Process(7001, [CreateRequest("input")]).Single();

        Assert.That(Encoding.UTF8.GetString((byte[])result.Body!), Is.EqualTo("processed"));
    }

    private static SocketServerState CreateState(
        SocketEndpointConfig[] endpoints,
        params (string Name, Func<Data<object>, Data<object>> Processor)[] stubs)
    {
        var mappedStubs = stubs.Length == 0
            ? ImmutableList<TransactionStub>.Empty
            : stubs.Select(tuple => CreateStub(tuple.Name, tuple.Processor)).ToImmutableList();

        return new SocketServerState(
            Globals.Logger,
            ImmutableList<DataSource>.Empty,
            mappedStubs,
            endpoints);
    }

    private static SocketEndpointConfig BuildEndpoint(
        int port,
        string actionName,
        SocketMethod method,
        string? dataSourceName = null,
        string? transactionStubName = null)
    {
        return new SocketEndpointConfig
        {
            Port = port,
            ProtocolType = ProtocolType.Tcp,
            TimeoutMs = 100,
            Action = new SocketActionConfig
            {
                Name = actionName,
                Method = method,
                DataSourceName = dataSourceName,
                TransactionStubName = transactionStubName
            }
        };
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

    private static Data<object> CreateRequest(string payload)
    {
        return new Data<object>
        {
            Body = Encoding.UTF8.GetBytes(payload),
            MetaData = new MetaData()
        };
    }

    private sealed class DelegateProcessor(Func<Data<object>, Data<object>> process) : ITransactionProcessor
    {
        public Context Context { get; set; } = null!;
        public List<ValidationResult>? LoadAndValidateConfiguration(IConfiguration configuration) => [];
        public Data<object> Process(IImmutableList<DataSource> dataSourceList, Data<object> requestData) => process(requestData);
    }
}
