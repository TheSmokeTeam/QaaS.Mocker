using System.Diagnostics;
using Elasticsearch.Net;
using Elasticsearch.Net.Diagnostics;

namespace QaaS.Mocker.Loaders;

/// <summary>Observes the existing sink's startup handshake to choose its CLI shutdown deadline.</summary>
internal sealed class ElasticSinkStartup : IObserver<DiagnosticListener>, IDisposable
{
    private readonly AsyncLocal<bool> _loading = new();
    private readonly IDisposable _sources;
    private readonly Lock _gate = new();
    private List<IDisposable>? _audits = [];
    private volatile bool _attempted;
    private volatile bool _connected;

    internal bool? Connected => _attempted ? _connected : null;

    internal ElasticSinkStartup()
    {
        _loading.Value = true;
        _sources = DiagnosticListener.AllListeners.Subscribe(this);
    }

    void IObserver<DiagnosticListener>.OnNext(DiagnosticListener source)
    {
        if (source.Name != DiagnosticSources.AuditTrailEvents.SourceName)
            return;
        lock (_gate)
            _audits?.Add(source.Subscribe(new AuditDiagnosticObserver(entry =>
            {
                // Another embedded loader can initialize an unrelated sink at the same time.
                if (!_loading.Value)
                    return;
                if (entry.Value.Event is AuditEvent.ProductCheckSuccess or AuditEvent.HealthyResponse)
                    _connected = true;
                _attempted = true;
            })));
    }

    void IObserver<DiagnosticListener>.OnError(Exception error) { }
    void IObserver<DiagnosticListener>.OnCompleted() { }

    public void Dispose()
    {
        _loading.Value = false;
        _sources.Dispose();
        List<IDisposable>? audits;
        lock (_gate)
        {
            audits = _audits;
            _audits = null;
        }
        if (audits != null)
            foreach (var audit in audits)
                audit.Dispose();
    }
}
