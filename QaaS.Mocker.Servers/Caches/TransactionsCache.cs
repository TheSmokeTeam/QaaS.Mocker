using System.Collections.Concurrent;
using System.Text;
using System.Text.Json;
using Google.Protobuf;
using QaaS.Framework.SDK.Extensions;
using QaaS.Framework.SDK.Session.DataObjects;

namespace QaaS.Mocker.Servers.Caches;

public class TransactionsCache : BaseCache<DetailedData<object>>
{
    private readonly ConcurrentQueue<DetailedData<object>> _inputQueue = new();
    private readonly ConcurrentQueue<DetailedData<object>?> _outputQueue = new();

    public override void StoreInput(DetailedData<object> item, string actionName )
    {
        if (EnableStorage == false) return;
        if (CachedAction != null && CachedAction != actionName) return;
        _inputQueue.Enqueue(item.FilterData(InputDataFilter));
    }

    public override void StoreOutput(DetailedData<object>? item, string actionName )
    {
        if (EnableStorage == false) return;
        if (CachedAction != null && CachedAction != actionName) return;
        _outputQueue.Enqueue(item?.FilterData(OutputDataFilter));
    }

    public override string? RetrieveFirstOrDefaultStringInput()
    {
        // A single TryDequeue keeps the consume path atomic under concurrent readers.
        if (!_inputQueue.TryDequeue(out var item)) return null;
        return JsonSerializer.Serialize(SerializeForRunner(item));
    }

    public override string? RetrieveFirstOrDefaultStringOutput()
    {
        // A single TryDequeue keeps the consume path atomic under concurrent readers.
        if (!_outputQueue.TryDequeue(out var item)) return null;
        return JsonSerializer.Serialize(SerializeForRunner(item));
    }

    private static DetailedData<byte[]>? SerializeForRunner(DetailedData<object>? item)
    {
        if (item is null) return null;

        return new DetailedData<byte[]>
        {
            Body = item.Body switch
            {
                null => null,
                byte[] bytes => bytes,
                ReadOnlyMemory<byte> memory => memory.ToArray(),
                IMessage protobufMessage => protobufMessage.ToByteArray(),
                string value => Encoding.UTF8.GetBytes(value),
                _ => JsonSerializer.SerializeToUtf8Bytes(item.Body, item.Body.GetType())
            },
            MetaData = item.MetaData,
            Timestamp = item.Timestamp
        };
    }
}
