using Google.Protobuf;
using NUnit.Framework;
using Google.Protobuf.WellKnownTypes;
using System.Text.Json;
using QaaS.Framework.SDK.Session.DataObjects;
using QaaS.Framework.SDK.Session.MetaDataObjects;
using QaaS.Mocker.Servers.Caches;

namespace QaaS.Mocker.Servers.Tests.CachesTests;

[TestFixture]
public class TransactionsCacheTests
{
    [Test]
    public void StoreInput_WhenStorageDisabled_DoesNotPersist()
    {
        var cache = new TransactionsCache { EnableStorage = false };

        cache.StoreInput(CreateDetailedData("one"), "ActionA");

        Assert.That(cache.RetrieveFirstOrDefaultStringInput(), Is.Null);
    }

    [Test]
    public void StoreOutput_WhenActionFilterDoesNotMatch_DoesNotPersist()
    {
        var cache = new TransactionsCache
        {
            EnableStorage = true,
            CachedAction = "ActionA"
        };

        cache.StoreOutput(CreateDetailedData("one"), "ActionB");

        Assert.That(cache.RetrieveFirstOrDefaultStringOutput(), Is.Null);
    }

    [Test]
    public void RetrieveFirstOrDefaultStringInput_ReturnsSerializedAndDequeues()
    {
        var cache = new TransactionsCache { EnableStorage = true };
        cache.StoreInput(CreateDetailedData("first"), "ActionA");
        cache.StoreInput(CreateDetailedData("second"), "ActionA");

        var first = GetBodyString(cache.RetrieveFirstOrDefaultStringInput());
        var second = GetBodyString(cache.RetrieveFirstOrDefaultStringInput());
        var third = cache.RetrieveFirstOrDefaultStringInput();

        Assert.Multiple(() =>
        {
            Assert.That(first, Is.EqualTo("first"));
            Assert.That(second, Is.EqualTo("second"));
            Assert.That(third, Is.Null);
        });
    }

    [Test]
    public void RetrieveFirstOrDefaultStringOutput_WhenNullItemStored_ReturnsNullJsonLiteral()
    {
        var cache = new TransactionsCache { EnableStorage = true };
        cache.StoreOutput(null, "ActionA");

        var result = cache.RetrieveFirstOrDefaultStringOutput();

        Assert.That(result, Is.EqualTo("null"));
    }

    [Test]
    public void RetrieveFirstOrDefaultStringInput_WhenBodyIsProtobufMessage_SerializesBodyAsByteArray()
    {
        var cache = new TransactionsCache { EnableStorage = true };
        cache.StoreInput(new DetailedData<object>
        {
            Timestamp = DateTime.UtcNow,
            Body = new StringValue { Value = "grpc-payload" },
            MetaData = new MetaData()
        }, "ActionA");

        var result = cache.RetrieveFirstOrDefaultStringInput();
        var deserialized = System.Text.Json.JsonSerializer.Deserialize<DetailedData<byte[]>>(result!);

        Assert.That(deserialized, Is.Not.Null);
        Assert.That(deserialized!.Body, Is.EqualTo(new StringValue { Value = "grpc-payload" }.ToByteArray()));
    }

    [Test]
    public async Task RetrieveFirstOrDefaultStringInput_WhenReadConcurrently_DequeuesEachItemOnce()
    {
        var cache = new TransactionsCache { EnableStorage = true };
        cache.StoreInput(CreateDetailedData("first"), "ActionA");
        cache.StoreInput(CreateDetailedData("second"), "ActionA");
        cache.StoreInput(CreateDetailedData("third"), "ActionA");

        var results = (await Task.WhenAll(Enumerable.Range(0, 3)
                .Select(_ => Task.Run(cache.RetrieveFirstOrDefaultStringInput))))
            .Select(GetBodyString)
            .ToArray();

        Assert.Multiple(() =>
        {
            Assert.That(results.Count(result => result != null), Is.EqualTo(3));
            Assert.That(results, Has.Exactly(1).EqualTo("first"));
            Assert.That(results, Has.Exactly(1).EqualTo("second"));
            Assert.That(results, Has.Exactly(1).EqualTo("third"));
        });
    }

    private static DetailedData<object> CreateDetailedData(string value)
    {
        return new DetailedData<object>
        {
            Timestamp = DateTime.UtcNow,
            Body = value,
            MetaData = new MetaData()
        };
    }

    private static string? GetBodyString(string? serializedItem)
    {
        if (serializedItem is null)
        {
            return null;
        }

        using var document = JsonDocument.Parse(serializedItem);
        return document.RootElement.GetProperty("Body").GetString();
    }
}
