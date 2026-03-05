using NUnit.Framework;
using QaaS.Mocker.Controller.Extensions;

namespace QaaS.Mocker.Controller.Tests.ExtensionsTests;

[TestFixture]
public class ChannelRouterExtensionsTests
{
    [Test]
    public void SubPingsChannel_ReturnsExpectedValue()
    {
        Assert.That(ChannelRouterExtensions.SubPingsChannel(), Is.EqualTo("runner:mocker:pings"));
    }

    [Test]
    public void SubCommandsChannel_UsesProvidedServerName()
    {
        Assert.That(ChannelRouterExtensions.SubCommandsChannel("svc-a"),
            Is.EqualTo("runner:mocker:commands:svc-a"));
    }

    [Test]
    public void PubAcks_ReturnsExpectedValue()
    {
        Assert.That(ChannelRouterExtensions.PubAcks(), Is.EqualTo("mocker:runner:acks"));
    }
}
