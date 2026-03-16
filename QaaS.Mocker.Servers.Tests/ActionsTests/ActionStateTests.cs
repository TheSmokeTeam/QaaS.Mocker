using NUnit.Framework;
using QaaS.Mocker.Servers.Actions;

namespace QaaS.Mocker.Servers.Tests.ActionsTests;

[TestFixture]
public class ActionStateTests
{
    [Test]
    public async Task SetEnabledForTimeoutMs_EnablesThenDisables()
    {
        var state = new ActionState<int> { State = 1 };

        var task = state.SetEnabledForTimeoutMs(60);
        await Task.Delay(15);
        var enabledWhileRunning = state.Enabled;
        await task;

        Assert.Multiple(() =>
        {
            Assert.That(enabledWhileRunning, Is.True);
            Assert.That(state.Enabled, Is.False);
        });
    }

    [Test]
    public async Task SetEnabledForTimeoutMs_ZeroTimeout_DisablesImmediately()
    {
        var state = new ActionState<int> { State = 1 };

        await state.SetEnabledForTimeoutMs(0);

        Assert.That(state.Enabled, Is.False);
    }

    [Test]
    public async Task SetEnabledForTimeoutMs_WhenTriggeredAgain_KeepsLatestWindowActive()
    {
        var state = new ActionState<int> { State = 1 };

        var firstTrigger = state.SetEnabledForTimeoutMs(40);
        await Task.Delay(10);
        var secondTrigger = state.SetEnabledForTimeoutMs(300);

        await firstTrigger;
        var enabledAfterFirstWindowElapsed = state.Enabled;
        await Task.Delay(100);
        var stillEnabledDuringSecondWindow = state.Enabled;
        await secondTrigger;

        Assert.Multiple(() =>
        {
            Assert.That(enabledAfterFirstWindowElapsed, Is.True);
            Assert.That(stillEnabledDuringSecondWindow, Is.True);
            Assert.That(state.Enabled, Is.False);
        });
    }
    [Test]
    public async Task SetEnabledForTimeoutMs_WhenDefaultEnabled_RestoresEnabledStateAfterTimeout()
    {
        var state = new ActionState<int> { State = 1, DefaultEnabled = true, Enabled = true };

        await state.SetEnabledForTimeoutMs(40);

        Assert.That(state.Enabled, Is.True);
    }
}
