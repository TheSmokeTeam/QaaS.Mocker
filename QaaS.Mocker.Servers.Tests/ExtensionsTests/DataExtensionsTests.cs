using NUnit.Framework;
using QaaS.Framework.SDK.Session.DataObjects;
using QaaS.Framework.SDK.Session.MetaDataObjects;
using QaaS.Mocker.Servers.Extensions;

namespace QaaS.Mocker.Servers.Tests.ExtensionsTests;

[TestFixture]
public class DataExtensionsTests
{
    [Test]
    public void CloneDetailed_WithoutTimestamp_UsesCurrentUtcTime()
    {
        var before = DateTime.UtcNow;
        var data = new Data<object> { Body = "payload", MetaData = new MetaData() };

        var clone = data.CloneDetailed();

        var after = DateTime.UtcNow;
        Assert.Multiple(() =>
        {
            Assert.That(clone.Body, Is.EqualTo("payload"));
            Assert.That(clone.MetaData, Is.SameAs(data.MetaData));
            Assert.That(clone.Timestamp, Is.InRange(before, after));
        });
    }

    [Test]
    public void CloneDetailed_WithExplicitTimestamp_UsesProvidedValue()
    {
        var timestamp = new DateTime(2026, 3, 15, 10, 30, 0, DateTimeKind.Utc);
        var data = new Data<object> { Body = "payload" };

        var clone = data.CloneDetailed(timestamp);

        Assert.That(clone.Timestamp, Is.EqualTo(timestamp));
    }
}
