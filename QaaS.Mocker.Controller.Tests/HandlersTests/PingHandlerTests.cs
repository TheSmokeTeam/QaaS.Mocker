using Microsoft.Extensions.Logging;
using Moq;
using NUnit.Framework;
using Qaas.Mocker.CommunicationObjects.ConfigurationObjects.Ping;
using QaaS.Mocker.Controller.Handlers;
using QaaS.Mocker.Servers.Caches;
using QaaS.Mocker.Servers.ServerStates;
using StackExchange.Redis;
using CommunicationInputOutputState = Qaas.Mocker.CommunicationObjects.ConfigurationObjects.InputOutputState;
using ServerInputOutputState = QaaS.Framework.SDK.ConfigurationObjects.InputOutputState;

namespace QaaS.Mocker.Controller.Tests.HandlersTests;

[TestFixture]
public class PingHandlerTests
{
    [Test]
    public void RequestAndResponseChannels_UseServerNameWithoutInstanceId()
    {
        var handler = CreateHandler("SERVER-A", "instance-1");

        Assert.Multiple(() =>
        {
            Assert.That(handler.GetRequestChannel(), Is.EqualTo("runner-to-mocker:ping:server-a"));
            Assert.That(handler.GetResponseChannel(), Is.EqualTo("mocker-to-runner:ping:server-a"));
        });
    }

    [TestCase(ServerInputOutputState.NoInputOutput, CommunicationInputOutputState.NoInputOutput)]
    [TestCase(ServerInputOutputState.OnlyInput, CommunicationInputOutputState.OnlyInput)]
    [TestCase(ServerInputOutputState.OnlyOutput, CommunicationInputOutputState.OnlyOutput)]
    [TestCase(ServerInputOutputState.BothInputOutput, CommunicationInputOutputState.BothInputOutput)]
    public void HandleRequest_ReturnsServerIdentityAndInputOutputState(
        ServerInputOutputState inputOutputState,
        CommunicationInputOutputState expectedResponseState)
    {
        var handler = CreateHandler("server-a", "instance-42", inputOutputState);

        var response = handler.Invoke(new PingRequest { Id = "ping-1" });

        Assert.Multiple(() =>
        {
            Assert.That(response, Is.Not.Null);
            Assert.That(response!.Id, Is.EqualTo("ping-1"));
            Assert.That(response.ServerName, Is.EqualTo("server-a"));
            Assert.That(response.ServerInstanceId, Is.EqualTo("instance-42"));
            Assert.That(response.ServerInputOutputState, Is.EqualTo(expectedResponseState));
        });
    }

    private static TestablePingHandler CreateHandler(
        string serverName,
        string serverInstanceId,
        ServerInputOutputState inputOutputState = ServerInputOutputState.BothInputOutput)
    {
        var cache = new Mock<ICache>();
        var serverState = new Mock<IServerState>();
        serverState.Setup(state => state.GetCache()).Returns(cache.Object);
        serverState.SetupGet(state => state.InputOutputState).Returns(inputOutputState);

        var subscriberClient = new Mock<ISubscriber>();
        return new TestablePingHandler(
            serverState.Object,
            subscriberClient.Object,
            serverName,
            serverInstanceId,
            Globals.Logger);
    }

    private sealed class TestablePingHandler(
        IServerState serverState,
        ISubscriber subscriberClient,
        string serverName,
        string serverInstanceId,
        ILogger logger)
        : PingHandler(serverState, subscriberClient, serverName, serverInstanceId, logger)
    {
        public string GetRequestChannel() => RequestChannel();
        public string GetResponseChannel() => ResponseChannel();
        public PingResponse? Invoke(PingRequest request) => HandleRequest("runner-to-mocker:ping", request);
    }
}
