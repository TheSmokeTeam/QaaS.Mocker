using System.Collections.Immutable;
using System.ComponentModel.DataAnnotations;
using System.Text;
using Microsoft.Extensions.Configuration;
using NUnit.Framework;
using QaaS.Framework.SDK.ContextObjects;
using QaaS.Framework.SDK.DataSourceObjects;
using QaaS.Framework.SDK.Hooks.Processor;
using QaaS.Framework.SDK.Session.DataObjects;
using QaaS.Framework.SDK.Session.MetaDataObjects;
using QaaS.Mocker.Servers.ConfigurationObjects.HttpServerConfigs;
using QaaS.Mocker.Servers.Exceptions;
using QaaS.Mocker.Servers.ServerStates;
using QaaS.Mocker.Stubs.Stubs;
using HttpMethod = QaaS.Mocker.Servers.ConfigurationObjects.HttpServerConfigs.HttpMethod;

namespace QaaS.Mocker.Servers.Tests.ServerStateTests;

[TestFixture]
public class HttpServerStateTests
{
    [Test]
    public void Process_WithKnownEndpoint_UsesMappedStubAndSetsPathParameters()
    {
        var state = CreateState(
            ("MainStub", _ => CreateResponse("main")),
            ("NotFoundStub", _ => CreateResponse("not-found")),
            ("InternalStub", _ => CreateResponse("internal")));

        var response = state.Process("/users/42", HttpMethod.Get, CreateRequestData());

        Assert.That(Encoding.UTF8.GetString((byte[])response.Body!), Is.EqualTo("main"));
    }

    [Test]
    public void Process_WithUnknownEndpoint_UsesNotFoundStub()
    {
        var state = CreateState(
            ("MainStub", _ => CreateResponse("main")),
            ("NotFoundStub", _ => CreateResponse("not-found")),
            ("InternalStub", _ => CreateResponse("internal")));

        var response = state.Process("/missing", HttpMethod.Get, CreateRequestData());

        Assert.That(Encoding.UTF8.GetString((byte[])response.Body!), Is.EqualTo("not-found"));
    }

    [Test]
    public void Process_WhenPrimaryStubThrows_UsesInternalErrorStub()
    {
        var state = CreateState(
            ("MainStub", _ => throw new InvalidOperationException("boom")),
            ("NotFoundStub", _ => CreateResponse("not-found")),
            ("InternalStub", _ => CreateResponse("internal")));

        var response = state.Process("/users/42", HttpMethod.Get, CreateRequestData());

        Assert.That(Encoding.UTF8.GetString((byte[])response.Body!), Is.EqualTo("internal"));
    }

    [Test]
    public void ChangeActionStub_WhenActionExists_ChangesRoutedStub()
    {
        var state = CreateState(
            ("MainStub", _ => CreateResponse("main")),
            ("AltStub", _ => CreateResponse("alt")),
            ("NotFoundStub", _ => CreateResponse("not-found")),
            ("InternalStub", _ => CreateResponse("internal")));

        state.ChangeActionStub("GetUser", "AltStub");
        var response = state.Process("/users/42", HttpMethod.Get, CreateRequestData());

        Assert.That(Encoding.UTF8.GetString((byte[])response.Body!), Is.EqualTo("alt"));
    }

    [Test]
    public void ChangeActionStub_WhenActionDoesNotExist_Throws()
    {
        var state = CreateState(
            ("MainStub", _ => CreateResponse("main")),
            ("NotFoundStub", _ => CreateResponse("not-found")),
            ("InternalStub", _ => CreateResponse("internal")));

        Assert.Throws<ActionDoesNotExistException>(() => state.ChangeActionStub("MissingAction", "MainStub"));
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

        _ = state.Process("/users/42", HttpMethod.Get, CreateRequestData());

        Assert.Multiple(() =>
        {
            Assert.That(cache.RetrieveFirstOrDefaultStringInput(), Is.Not.Null);
            Assert.That(cache.RetrieveFirstOrDefaultStringOutput(), Is.Not.Null);
        });
    }

    private static HttpServerState CreateState(params (string Name, Func<Data<object>, Data<object>> Processor)[] stubs)
    {
        var endpoint = new HttpEndpointConfig
        {
            Path = "/users/{id}",
            Actions =
            [
                new HttpEndpointActionConfig
                {
                    Name = "GetUser",
                    Method = HttpMethod.Get,
                    TransactionStubName = "MainStub"
                }
            ]
        };

        return new HttpServerState(
            Globals.Logger,
            stubs.Select(tuple => CreateStub(tuple.Name, tuple.Processor)).ToImmutableList(),
            "NotFoundStub",
            "InternalStub",
            [endpoint]);
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
            Body = Encoding.UTF8.GetBytes(payload),
            MetaData = new MetaData { Http = new Http { StatusCode = 200 } }
        };
    }

    private static Data<object> CreateRequestData()
    {
        return new Data<object>
        {
            Body = Array.Empty<byte>(),
            MetaData = new MetaData { Http = new Http() }
        };
    }

    private sealed class DelegateProcessor(Func<Data<object>, Data<object>> process) : ITransactionProcessor
    {
        public Context Context { get; set; } = null!;
        public List<ValidationResult>? LoadAndValidateConfiguration(IConfiguration configuration) => [];
        public Data<object> Process(IImmutableList<DataSource> dataSourceList, Data<object> requestData) => process(requestData);
    }
}
