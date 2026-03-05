using System.Text.Json;
using Microsoft.Extensions.Logging;
using Moq;
using NUnit.Framework;
using QaaS.Mocker.Controller.Handlers;
using StackExchange.Redis;

namespace QaaS.Mocker.Controller.Tests.HandlersTests;

[TestFixture]
public class BaseHandlerTests
{
    [Test]
    public void Start_WhenMessageIsEmpty_DoesNotHandleOrPublish()
    {
        var subscriberClient = CreateSubscriberClient(out var probe);
        var handler = new TestHandler(subscriberClient.Object, Globals.Logger);
        handler.Start();

        probe.SubscribedHandler!.Invoke("runner:mocker:test", string.Empty);

        Assert.Multiple(() =>
        {
            Assert.That(handler.HandledRequestsCount, Is.EqualTo(0));
            Assert.That(handler.LastRequest, Is.Null);
        });
        subscriberClient.Verify(client =>
                client.Publish(It.IsAny<RedisChannel>(), It.IsAny<RedisValue>(), It.IsAny<CommandFlags>()),
            Times.Never);
    }

    [Test]
    public void Start_WhenMessageIsInvalidJson_DoesNotHandleOrPublish()
    {
        var subscriberClient = CreateSubscriberClient(out var probe);
        var handler = new TestHandler(subscriberClient.Object, Globals.Logger);
        handler.Start();

        probe.SubscribedHandler!.Invoke("runner:mocker:test", "{invalid-json");

        Assert.Multiple(() =>
        {
            Assert.That(handler.HandledRequestsCount, Is.EqualTo(0));
            Assert.That(handler.LastRequest, Is.Null);
        });
        subscriberClient.Verify(client =>
                client.Publish(It.IsAny<RedisChannel>(), It.IsAny<RedisValue>(), It.IsAny<CommandFlags>()),
            Times.Never);
    }

    [Test]
    public void Start_WhenJsonBodyIsNull_DoesNotHandleOrPublish()
    {
        var subscriberClient = CreateSubscriberClient(out var probe);
        var handler = new TestHandler(subscriberClient.Object, Globals.Logger);
        handler.Start();

        probe.SubscribedHandler!.Invoke("runner:mocker:test", "null");

        Assert.Multiple(() =>
        {
            Assert.That(handler.HandledRequestsCount, Is.EqualTo(0));
            Assert.That(handler.LastRequest, Is.Null);
        });
        subscriberClient.Verify(client =>
                client.Publish(It.IsAny<RedisChannel>(), It.IsAny<RedisValue>(), It.IsAny<CommandFlags>()),
            Times.Never);
    }

    [Test]
    public void Start_WhenMessageIsValid_HandlesAndPublishesResponse()
    {
        var subscriberClient = CreateSubscriberClient(out var probe);
        var handler = new TestHandler(subscriberClient.Object, Globals.Logger);
        handler.Start();

        probe.SubscribedHandler!.Invoke("runner:mocker:test",
            JsonSerializer.Serialize(new TestRequest { Value = "hello" }));

        Assert.Multiple(() =>
        {
            Assert.That(handler.HandledRequestsCount, Is.EqualTo(1));
            Assert.That(handler.LastRequest, Is.Not.Null);
            Assert.That(handler.LastRequest!.Value, Is.EqualTo("hello"));
            Assert.That(probe.PublishedPayload, Is.Not.Null);
            Assert.That(probe.PublishedPayload, Does.Contain("hello"));
        });
        subscriberClient.Verify(client =>
                client.Publish(It.IsAny<RedisChannel>(), It.IsAny<RedisValue>(), It.IsAny<CommandFlags>()),
            Times.Once);
    }

    private static Mock<ISubscriber> CreateSubscriberClient(
        out SubscriberProbe probe)
    {
        var localProbe = new SubscriberProbe();
        probe = localProbe;
        var subscriberClient = new Mock<ISubscriber>();
        subscriberClient
            .Setup(client => client.Subscribe(
                It.IsAny<RedisChannel>(),
                It.IsAny<Action<RedisChannel, RedisValue>>(),
                It.IsAny<CommandFlags>()))
            .Callback<RedisChannel, Action<RedisChannel, RedisValue>, CommandFlags>((_, handler, _) =>
                localProbe.SubscribedHandler = handler);
        subscriberClient
            .Setup(client => client.Publish(
                It.IsAny<RedisChannel>(),
                It.IsAny<RedisValue>(),
                It.IsAny<CommandFlags>()))
            .Callback<RedisChannel, RedisValue, CommandFlags>((_, payload, _) =>
                localProbe.PublishedPayload = payload.ToString())
            .Returns(1);
        return subscriberClient;
    }

    private sealed class SubscriberProbe
    {
        public Action<RedisChannel, RedisValue>? SubscribedHandler { get; set; }
        public string? PublishedPayload { get; set; }
    }

    private sealed class TestHandler(ISubscriber subscriberClient, ILogger logger)
        : BaseHandler<TestRequest, TestResponse>(subscriberClient, "server-a", "instance-1", logger)
    {
        public int HandledRequestsCount { get; private set; }
        public TestRequest? LastRequest { get; private set; }

        protected override string ContentType => "test";

        protected override TestResponse? HandleRequest(RedisChannel channel, TestRequest requestMessage)
        {
            HandledRequestsCount++;
            LastRequest = requestMessage;
            return new TestResponse { Echo = requestMessage.Value };
        }
    }

    private sealed record TestRequest
    {
        public string? Value { get; init; }
    }

    private sealed record TestResponse
    {
        public string? Echo { get; init; }
    }
}
