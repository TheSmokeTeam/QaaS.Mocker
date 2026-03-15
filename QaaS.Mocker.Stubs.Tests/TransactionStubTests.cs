using System.Collections.Immutable;
using System.Text;
using Google.Protobuf.WellKnownTypes;
using Moq;
using NUnit.Framework;
using QaaS.Framework.SDK.DataSourceObjects;
using QaaS.Framework.SDK.Hooks.Processor;
using QaaS.Framework.SDK.Session.DataObjects;
using QaaS.Framework.Serialization.Deserializers;
using QaaS.Framework.Serialization.Serializers;
using QaaS.Mocker.Stubs.Stubs;
using Type = System.Type;

namespace QaaS.Mocker.Stubs.Tests;

[TestFixture]
public class TransactionStubTests
{
    [Test]
    public void Exercise_WhenNoSerializationConfigured_ReturnsProcessorResponse()
    {
        var processor = CreateProcessor(_ => new Data<object> { Body = Encoding.UTF8.GetBytes("ok") });
        var stub = new TransactionStub
        {
            Name = "StubA",
            Processor = processor.Object,
            DataSourceList = ImmutableList<DataSource>.Empty
        };

        var result = stub.Exercise(new Data<object> { Body = Encoding.UTF8.GetBytes("request") });

        Assert.That(Encoding.UTF8.GetString((byte[])result.Body!), Is.EqualTo("ok"));
    }

    [Test]
    public void Exercise_WhenRequestDeserializerConfiguredAndBodyIsNotBytes_ThrowsArgumentException()
    {
        var processor = CreateProcessor(data => data);
        var deserializer = new Mock<IDeserializer>();
        var stub = new TransactionStub
        {
            Name = "StubA",
            Processor = processor.Object,
            DataSourceList = ImmutableList<DataSource>.Empty,
            RequestBodyDeserializer = deserializer.Object
        };

        Assert.Throws<ArgumentException>(() => stub.Exercise(new Data<object> { Body = "not-bytes" }));
    }

    [Test]
    public void Exercise_WhenRequestDeserializerConfigured_DeserializesBeforeProcessorCall()
    {
        var processor = CreateProcessor(data =>
        {
            Assert.That(data.Body, Is.EqualTo("deserialized-body"));
            return new Data<object> { Body = Encoding.UTF8.GetBytes("ok") };
        });

        var deserializer = new Mock<IDeserializer>();
        deserializer
            .Setup(instance => instance.Deserialize(It.IsAny<byte[]>(), It.IsAny<Type>()))
            .Returns("deserialized-body");

        var stub = new TransactionStub
        {
            Name = "StubA",
            Processor = processor.Object,
            DataSourceList = ImmutableList<DataSource>.Empty,
            RequestBodyDeserializer = deserializer.Object
        };

        _ = stub.Exercise(new Data<object> { Body = Encoding.UTF8.GetBytes("request") });

        deserializer.Verify(instance => instance.Deserialize(It.IsAny<byte[]>(), It.IsAny<Type>()), Times.Once);
    }

    [Test]
    public void Exercise_WhenRequestDeserializerConfiguredAndBodyIsProtobuf_DeserializesSerializedPayload()
    {
        var processor = CreateProcessor(data =>
        {
            Assert.That(data.Body, Is.EqualTo("protobuf-body"));
            return new Data<object> { Body = Encoding.UTF8.GetBytes("ok") };
        });

        var deserializer = new Mock<IDeserializer>();
        deserializer
            .Setup(instance => instance.Deserialize(It.IsAny<byte[]>(), It.IsAny<Type>()))
            .Returns("protobuf-body");

        var stub = new TransactionStub
        {
            Name = "StubA",
            Processor = processor.Object,
            DataSourceList = ImmutableList<DataSource>.Empty,
            RequestBodyDeserializer = deserializer.Object
        };

        _ = stub.Exercise(new Data<object> { Body = new StringValue { Value = "request" } });

        deserializer.Verify(instance => instance.Deserialize(It.IsAny<byte[]>(), It.IsAny<Type>()), Times.Once);
    }

    [Test]
    public void Exercise_WhenRequestDeserializerConfiguredAndBodyIsReadOnlyMemory_DeserializesSerializedPayload()
    {
        var processor = CreateProcessor(data =>
        {
            Assert.That(data.Body, Is.EqualTo("memory-body"));
            return new Data<object> { Body = Encoding.UTF8.GetBytes("ok") };
        });

        var deserializer = new Mock<IDeserializer>();
        deserializer
            .Setup(instance => instance.Deserialize(It.IsAny<byte[]>(), It.IsAny<Type>()))
            .Returns("memory-body");

        var stub = new TransactionStub
        {
            Name = "StubA",
            Processor = processor.Object,
            DataSourceList = ImmutableList<DataSource>.Empty,
            RequestBodyDeserializer = deserializer.Object
        };

        _ = stub.Exercise(new Data<object> { Body = new ReadOnlyMemory<byte>(Encoding.UTF8.GetBytes("request")) });

        deserializer.Verify(instance => instance.Deserialize(It.IsAny<byte[]>(), It.IsAny<Type>()), Times.Once);
    }

    [Test]
    public void Exercise_WhenResponseSerializerConfigured_SerializesProcessorBody()
    {
        var processor = CreateProcessor(_ => new Data<object> { Body = new { Value = 1 } });
        var serializer = new Mock<ISerializer>();
        serializer.Setup(instance => instance.Serialize(It.IsAny<object>())).Returns(Encoding.UTF8.GetBytes("serialized"));

        var stub = new TransactionStub
        {
            Name = "StubA",
            Processor = processor.Object,
            DataSourceList = ImmutableList<DataSource>.Empty,
            ResponseBodySerializer = serializer.Object
        };

        var result = stub.Exercise(new Data<object> { Body = Encoding.UTF8.GetBytes("request") });

        Assert.Multiple(() =>
        {
            serializer.Verify(instance => instance.Serialize(It.IsAny<object>()), Times.Once);
            Assert.That(Encoding.UTF8.GetString((byte[])result.Body!), Is.EqualTo("serialized"));
        });
    }

    [Test]
    public void Exercise_WhenResponseBodyIsNull_ReturnsNullBodyWithoutThrowing()
    {
        var processor = CreateProcessor(_ => new Data<object> { Body = null });
        var stub = new TransactionStub
        {
            Name = "StubA",
            Processor = processor.Object,
            DataSourceList = ImmutableList<DataSource>.Empty
        };

        var result = stub.Exercise(new Data<object> { Body = Encoding.UTF8.GetBytes("request") });

        Assert.That(result.Body, Is.Null);
    }

    [Test]
    public void Exercise_WhenProcessorReturnsNonByteBodyAndNoSerializerConfigured_ThrowsArgumentException()
    {
        var processor = CreateProcessor(_ => new Data<object> { Body = "string-body" });
        var stub = new TransactionStub
        {
            Name = "StubA",
            Processor = processor.Object,
            DataSourceList = ImmutableList<DataSource>.Empty
        };

        Assert.Throws<ArgumentException>(() => stub.Exercise(new Data<object> { Body = Encoding.UTF8.GetBytes("request") }));
    }

    private static Mock<ITransactionProcessor> CreateProcessor(Func<Data<object>, Data<object>> process)
    {
        var processor = new Mock<ITransactionProcessor>();
        processor
            .Setup(instance => instance.Process(It.IsAny<IImmutableList<DataSource>>(), It.IsAny<Data<object>>()))
            .Returns((IImmutableList<DataSource> _, Data<object> request) => process(request));
        return processor;
    }
}
