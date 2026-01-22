using System.Collections.Concurrent;
using System.Text.Json;
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
        if (!_inputQueue.TryPeek(out var item)) return null;
        _inputQueue.TryDequeue(out item);
        return JsonSerializer.Serialize(item);
    }

    public override string? RetrieveFirstOrDefaultStringOutput()
    {
        if (!_outputQueue.TryPeek(out var item)) return null;
        _outputQueue.TryDequeue(out item);
        return JsonSerializer.Serialize(item);
    }
}