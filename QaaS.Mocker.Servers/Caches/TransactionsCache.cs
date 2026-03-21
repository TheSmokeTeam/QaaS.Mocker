using System.Collections.Concurrent;
using System.Text.Json;
using Google.Protobuf;
using QaaS.Framework.SDK.Extensions;
using QaaS.Framework.SDK.Session.DataObjects;

namespace QaaS.Mocker.Servers.Caches;

/// <summary>
/// In-memory cache for captured transaction input and output payloads.
/// </summary>
public class TransactionsCache : BaseCache<DetailedData<object>>
{
    private readonly ConcurrentQueue<DetailedData<object>> _inputQueue = new();
    private readonly ConcurrentQueue<DetailedData<object>?> _outputQueue = new();

    public override void StoreInput(DetailedData<object> item, string actionName)
    {
        if (!EnableStorage)
            return;
        if (CachedAction != null && CachedAction != actionName)
            return;

        _inputQueue.Enqueue(item.FilterData(InputDataFilter));
    }

    public override void StoreOutput(DetailedData<object>? item, string actionName)
    {
        if (!EnableStorage)
            return;
        if (CachedAction != null && CachedAction != actionName)
            return;

        _outputQueue.Enqueue(item?.FilterData(OutputDataFilter));
    }

    public override string? RetrieveFirstOrDefaultStringInput()
    {
        // A single TryDequeue keeps the consume path atomic under concurrent readers.
        if (!_inputQueue.TryDequeue(out var item))
            return null;
        return JsonSerializer.Serialize(SerializeForRunner(item));
    }

    public override string? RetrieveFirstOrDefaultStringOutput()
    {
        // A single TryDequeue keeps the consume path atomic under concurrent readers.
        if (!_outputQueue.TryDequeue(out var item))
            return null;
        return JsonSerializer.Serialize(SerializeForRunner(item));
    }

    private static object? SerializeForRunner(DetailedData<object>? item)
    {
        if (item is null)
            return null;

        return item.Body switch
        {
            ReadOnlyMemory<byte> memory => CreateSerializedBinaryItem(item, memory.ToArray()),
            IMessage protobufMessage => CreateSerializedBinaryItem(item, protobufMessage.ToByteArray()),
            _ => item
        };
    }

    private static DetailedData<byte[]> CreateSerializedBinaryItem(DetailedData<object> item, byte[] body)
    {
        return new DetailedData<byte[]>
        {
            Body = body,
            MetaData = item.MetaData,
            Timestamp = item.Timestamp
        };
    }
}
