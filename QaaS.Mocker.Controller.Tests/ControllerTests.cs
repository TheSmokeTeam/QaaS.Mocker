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
}
