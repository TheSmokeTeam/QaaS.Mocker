using NUnit.Framework;
using Moq;
using QaaS.Mocker.Servers.Actions;

namespace QaaS.Mocker.Servers.Tests.ActionsTests;

[TestFixture]
public class ActionStateTests
{
    [Test]
    public async Task SetEnabledForTimeoutMs_EnablesThenDisables()
    {
        var alarms = new Queue<Action>();
        var state = WithManualClock(alarms);

        var task = state.SetEnabledForTimeoutMs(60);
        var enabledWhileRunning = state.Enabled;
        alarms.Dequeue()();
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
        var alarms = new Queue<Action>();
        var state = WithManualClock(alarms);

        var firstTrigger = state.SetEnabledForTimeoutMs(40);
        var secondTrigger = state.SetEnabledForTimeoutMs(300);

        alarms.Dequeue()();
        await firstTrigger;
        var enabledAfterFirstWindowElapsed = state.Enabled;
        alarms.Dequeue()();
        await secondTrigger;

        Assert.Multiple(() =>
        {
            Assert.That(enabledAfterFirstWindowElapsed, Is.True);
            Assert.That(state.Enabled, Is.False);
        });
    }

    private static ActionState<int> WithManualClock(Queue<Action> alarms)
    {
        var clock = new Mock<TimeProvider>();
        clock.Setup(x => x.CreateTimer(It.IsAny<TimerCallback>(), It.IsAny<object?>(),
                It.IsAny<TimeSpan>(), It.IsAny<TimeSpan>()))
            .Returns((TimerCallback callback, object? state, TimeSpan _, TimeSpan _) =>
            {
                alarms.Enqueue(() => callback(state));
                return Mock.Of<ITimer>();
            });
        return new ActionState<int> { State = 1, Clock = clock.Object };
    }

    [Test]
    public async Task SetEnabledForTimeoutMs_WhenDefaultEnabled_RestoresEnabledStateAfterTimeout()
    {
        var state = new ActionState<int> { State = 1, DefaultEnabled = true, Enabled = true };

        await state.SetEnabledForTimeoutMs(40);

        Assert.That(state.Enabled, Is.True);
    }
}
