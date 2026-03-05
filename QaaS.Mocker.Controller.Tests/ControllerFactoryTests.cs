using Moq;
using NUnit.Framework;
using QaaS.Mocker.Controller.ConfigurationObjects;
using QaaS.Mocker.Servers.Caches;
using QaaS.Mocker.Servers.ServerStates;

namespace QaaS.Mocker.Controller.Tests;

[TestFixture]
public class ControllerFactoryTests
{
    [Test]
    public void Build_WithNullControllerConfig_ReturnsNull()
    {
        var factory = new ControllerFactory(Globals.Context, null);

        var result = factory.Build(CreateServerStateMock().Object);

        Assert.That(result, Is.Null);
    }

    [Test]
    public void Build_WithMissingServerName_ReturnsNull()
    {
        var factory = new ControllerFactory(Globals.Context, new ControllerConfig
        {
            Redis = new RedisConfig { Host = "127.0.0.1:1" }
        });

        var result = factory.Build(CreateServerStateMock().Object);

        Assert.That(result, Is.Null);
    }

    [Test]
    public void Build_WithMissingRedisConfig_ReturnsNull()
    {
        var factory = new ControllerFactory(Globals.Context, new ControllerConfig
        {
            ServerName = "mocker-a",
            Redis = null
        });

        var result = factory.Build(CreateServerStateMock().Object);

        Assert.That(result, Is.Null);
    }

    [Test]
    public void Build_WithInvalidRedisEndpoint_ReturnsNull()
    {
        var factory = new ControllerFactory(Globals.Context, new ControllerConfig
        {
            ServerName = "mocker-a",
            Redis = new RedisConfig
            {
                Host = "127.0.0.1:1",
                AbortOnConnectFail = true,
                ConnectRetry = 0,
                AsyncTimeout = 50
            }
        });

        var result = factory.Build(CreateServerStateMock().Object);

        Assert.That(result, Is.Null);
    }

    private static Mock<IServerState> CreateServerStateMock()
    {
        var serverState = new Mock<IServerState>();
        serverState.Setup(state => state.GetCache()).Returns(new Mock<ICache>().Object);
        return serverState;
    }
}
