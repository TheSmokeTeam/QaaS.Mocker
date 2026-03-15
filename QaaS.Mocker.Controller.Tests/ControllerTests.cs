using Moq;
using NUnit.Framework;
using QaaS.Mocker.Controller.Controllers;
using QaaS.Mocker.Servers.ServerStates;
using StackExchange.Redis;

namespace QaaS.Mocker.Controller.Tests;

[TestFixture]
public class ControllerTests
{
    [Test]
    public void Dispose_DisposesRedisConnection()
    {
        var redisConnection = new Mock<IConnectionMultiplexer>();
        var serverState = new Mock<IServerState>();
        var controller = new QaaS.Mocker.Controller.Controllers.Controller(
            redisConnection.Object,
            redisDataBase: 0,
            serverState.Object,
            serverName: "server-a",
            serverInstanceId: "instance-1",
            logger: Globals.Logger);

        controller.Dispose();

        redisConnection.Verify(connection => connection.Dispose(), Times.Once);
    }

    [Test]
    public void Start_WhenHandlerSubscriptionFails_PropagatesExceptionBeforeSleep()
    {
        var subscriber = new Mock<ISubscriber>();
        subscriber
            .Setup(instance => instance.Subscribe(
                It.IsAny<RedisChannel>(),
                It.IsAny<Action<RedisChannel, RedisValue>>(),
                It.IsAny<CommandFlags>()))
            .Throws(new InvalidOperationException("boom"));

        var database = new Mock<IDatabase>();
        var redisConnection = new Mock<IConnectionMultiplexer>();
        redisConnection
            .Setup(instance => instance.GetSubscriber(It.IsAny<object?>()))
            .Returns(subscriber.Object);
        redisConnection
            .Setup(instance => instance.GetDatabase(It.IsAny<int>(), It.IsAny<object?>()))
            .Returns(database.Object);
        redisConnection.SetupGet(instance => instance.Configuration).Returns("localhost:6379");

        var controller = new QaaS.Mocker.Controller.Controllers.Controller(
            redisConnection.Object,
            redisDataBase: 0,
            new Mock<IServerState>().Object,
            serverName: "server-a",
            serverInstanceId: "instance-1",
            logger: Globals.Logger);

        var exception = Assert.Throws<InvalidOperationException>(() => controller.Start());

        Assert.That(exception!.Message, Is.EqualTo("boom"));
    }
}
