using System.Diagnostics;
using Elasticsearch.Net.Diagnostics;

namespace QaaS.Mocker.Loaders;

/// <summary>Detects a successful response to an existing Elasticsearch sink request during CLI startup.</summary>
internal sealed class ElasticSinkStartup : IObserver<DiagnosticListener>, IDisposable
{
    private readonly AsyncLocal<bool> _loading = new();
    private readonly IDisposable _sources;
    private readonly Lock _gate = new();
    private List<IDisposable>? _subscriptions = [];
    private volatile bool _connected;

    internal bool Connected => _connected;

    internal ElasticSinkStartup()
    {
        _loading.Value = true;
        _sources = DiagnosticListener.AllListeners.Subscribe(this);
    }

    void IObserver<DiagnosticListener>.OnNext(DiagnosticListener source)
    {
        if (source.Name != DiagnosticSources.RequestPipeline.SourceName)
            return;
        lock (_gate)
            _subscriptions?.Add(source.Subscribe(new RequestPipelineDiagnosticObserver(null, completion =>
            {
                // The end event carries the actual response. Scope it to this loader's connection.
                if (_loading.Value && completion.Value is { Success: true, HttpStatusCode: >= 200 and < 300 })
                    _connected = true;
            })));
    }

    void IObserver<DiagnosticListener>.OnError(Exception error) { }
    void IObserver<DiagnosticListener>.OnCompleted() { }

    public void Dispose()
    {
        _loading.Value = false;
        _sources.Dispose();
        List<IDisposable>? subscriptions;
        lock (_gate)
        {
            subscriptions = _subscriptions;
            _subscriptions = null;
        }
        if (subscriptions != null)
            foreach (var subscription in subscriptions)
                subscription.Dispose();
    }
}
