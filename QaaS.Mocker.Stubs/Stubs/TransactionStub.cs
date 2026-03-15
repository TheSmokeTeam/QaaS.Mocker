using System.Collections.Immutable;
using Google.Protobuf;
using QaaS.Framework.SDK.DataSourceObjects;
using QaaS.Framework.SDK.Hooks.Processor;
using QaaS.Framework.SDK.Session.DataObjects;
using QaaS.Framework.Serialization.Deserializers;
using QaaS.Framework.Serialization.Serializers;

namespace QaaS.Mocker.Stubs.Stubs;

public class TransactionStub
{
    /// <summary>
    /// The name of the data source used to identify it
    /// </summary>
    public string Name { get; init; } = null!;

    /// <summary>
    /// The list of data sources relevant to the data source
    /// </summary>
    public IImmutableList<DataSource> DataSourceList { get; set; } = null!;

    /// <summary>
    /// The data source's generator
    /// </summary>
    public ITransactionProcessor Processor { get; init; } = null!;

    /// <summary>
    /// The Deserializer that will deserialize the input data, null means no deserialization.
    /// </summary>
    public IDeserializer? RequestBodyDeserializer { get; init; }

    /// <summary>
    /// A Specific type that the deserializer will deserialize the data to.
    /// </summary>
    public Type? RequestBodyDeserializerSpecificType { get; init; }

    /// <summary>
    /// The Serializer that will serialize the output data, null means no serialization.
    /// </summary>
    public ISerializer? ResponseBodySerializer { get; init; }

    /// <summary>
    /// Exercises this transaction stub's data according to given request.
    /// Request deserialization accepts both raw transport bytes and protobuf messages so HTTP and
    /// gRPC actions can share the same stub configuration.
    /// </summary>
    /// <returns> The response data from this stub </returns>
    public Data<object> Exercise(Data<object> requestData)
    {
        if (RequestBodyDeserializer is not null)
        {
            // gRPC requests arrive as protobuf message instances, while HTTP/socket requests usually
            // arrive as raw bytes. Convert both shapes to a byte[] before invoking the deserializer.
            var inputBodyByteArray = requestData.Body switch
            {
                byte[] bytes => bytes,
                ReadOnlyMemory<byte> memory => memory.ToArray(),
                IMessage protobufMessage => protobufMessage.ToByteArray(),
                _ => throw new ArgumentException($"Given Request body to Transaction Stub {Name}" +
                                                 " with request body deserialization is not byte[]")
            };

            requestData = new Data<object>
            {
                MetaData = requestData.MetaData,
                Body = RequestBodyDeserializer.Deserialize(inputBodyByteArray, RequestBodyDeserializerSpecificType)
            };
        }

        var responseData = Processor.Process(DataSourceList, requestData);
        if (responseData.Body == null) return responseData;

        if (ResponseBodySerializer is not null)
            responseData = new Data<object>
            {
                MetaData = responseData.MetaData,
                Body = ResponseBodySerializer.Serialize(responseData.Body)
            };

        // Server transports expect the final response payload to be a byte[] that can be written
        // directly back to the client.
        if (responseData.Body is not byte[])
            throw new ArgumentException($"Transaction Stub '{Name}' " +
                                        $"output is not byte[] for response payload");

        return responseData;
    }
}
