# Optional Mocker HTTP metrics

Set `EnableHttpMetrics: true` and `HttpMetricsSlot: 1` under an HTTP server
(`Server.Http` or the corresponding item under `Servers`). Metrics are off by
default; a disabled server ignores its slot. For example:

```yaml
Server:
  Http:
    Port: 8080
    EnableHttpMetrics: true
    HttpMetricsSlot: 1
    # Endpoints and stubs go here as usual.
```

The switch affects only HTTP/HTTPS handlers for that server and does **not** add
a route, open a metrics port, change
logging, or install/configure an exporter or observer. It does not emit from gRPC,
sockets, or requests rejected by Kestrel before this handler. A configured mock
readiness route still behaves like any other configured route; use a probe outside
this handler if you need a denominator excluding probe traffic.

The meter is `QaaS.Mocker.Http`. Each opted-in server owns its own counters;
a common publisher emits them with a bounded numeric `metric_slot` label and
tags each histogram observation with that same slot. Opted-in servers must use
distinct slots **1–8 within one process**, including across concurrent execution
builders. A missing, invalid, or already-reserved slot rejects the attempted
configuration explicitly. This does not alter an already-running owner's state
or responses.

The constructor checks range and any existing owner but does not claim or
export a slot. Startup claims it atomically, and pre-bind failure releases
that unexported reservation so an embedded caller can retry. Starting a second
server with the same slot rejects it even if both objects were constructed
before either started. Once the listener starts (or the first real request
reaches it), that slot is permanent for the process, including after shutdown:
reassignment would make historic samples indistinguishable from a counter
reset. Starting an activated slot again requires a fresh process, where it
can be reused with zero counters. In the CLI,
startup faults terminate the run; there is no automatic retry. A control-plane
stub swap does not construct a new server or change its slot. The limit applies
only to opted-in servers; use separately identified processes for more than eight.
An observer that throws nonfatally during instrument publication disables this
publisher rather than preventing a correctly configured server from starting.
Only consumers that elect to listen receive the measurements:

| .NET instrument | Type | Expected Prometheus name with suffix translation | Semantics |
|---|---|---|---|
| `qaas.mocker.requests.accepted` | Cumulative counter | `qaas_mocker_requests_accepted_total{metric_slot}` | Requests entering the opted-in handler |
| `qaas.mocker.requests.completed` | Cumulative counter | `qaas_mocker_requests_completed_total{metric_slot,outcome}` | Exactly one terminal handling outcome: `hit`, `miss`, or `error` |
| `qaas.mocker.request.duration` | Histogram, seconds | `qaas_mocker_request_duration_seconds_{bucket,sum,count}` | Monotonic handling duration; includes failed handlers |

A routed and successfully handled configured 404 is a **hit**. A route or known-method
mismatch handled by the not-found stub is a **miss**, even if that stub returns 200.
A processing/response error wins over the match classification, including when the
configured error fallback returns 202. A deliberate successful stub response is
not an internal failure solely because its HTTP status is 4xx/5xx. Counters use the
actual dispatch path, do not rely on existing log messages, and expose no configured
route, stub, address, port, path, body, header, identity, status, or exception labels.
Histogram advice asks for the common boundaries `0.05, 0.1, 0.25, 0.5, 1, 2.5, 5, 15`
seconds (plus +Inf); verify an alternate SDK honors the advice or use a matching
explicit-bucket view. This is 15 application series per active slot with those
buckets, up to **120 application series per full eight-slot process**, plus
scrape and exporter metadata. Disabled servers do not reserve, count or time any
slots. Non-raw dimensions are only the explicit bounded slot and fixed outcome;
Prometheus adds target topology.

## Attach an exporter separately

An existing .NET OpenTelemetry host can opt in with
`AddMeter("QaaS.Mocker.Http")` and its chosen exporter. For example, with compatible
`OpenTelemetry` and `OpenTelemetry.Exporter.OpenTelemetryProtocol` packages:

```csharp
using var provider = Sdk.CreateMeterProviderBuilder()
    .AddMeter("QaaS.Mocker.Http")
    .AddOtlpExporter(options =>
    {
        options.Protocol = OtlpExportProtocol.HttpProtobuf;
        options.Endpoint = new Uri("http://127.0.0.1:14318/v1/metrics");
    })
    .Build();
// Keep the provider alive for the lifetime of the Mocker host.
```

For an already installed and authorized OpenTelemetry .NET automatic instrumentation
agent on the CLI process, add `QaaS.Mocker.Http` to
`OTEL_DOTNET_AUTO_METRICS_ADDITIONAL_SOURCES` and configure an approved metrics OTLP
destination. To select the loopback example, set
`OTEL_METRICS_EXPORTER=otlp`, `OTEL_EXPORTER_OTLP_METRICS_PROTOCOL=http/protobuf`,
and `OTEL_EXPORTER_OTLP_METRICS_ENDPOINT=http://127.0.0.1:14318/v1/metrics` in that
process. These supplementary settings assume the instrumentation agent is already
correctly loaded; the variables alone do not start instrumentation. Limit any other
auto-instrumentation and verify permitted data before enabling an exporter.

The included [Collector example](otel-collector.yaml) for Collector Contrib v0.165.0
binds OTLP and the Prometheus exporter only to loopback and filters this pipeline to
the three listed instruments. The [scrape sample](prometheus.yaml) sets the dashboard's
`qaas_operational` job. Use an independently identified reader for each Mocker
process; don't combine independent cumulative streams with indistinguishable identity
into one exporter series. In a shared deployment, review transport security, access,
scrape identity and scrape budgets before deployment. The loopback example does not
change or enable a production exporter. Check the names and buckets against the
actual output of your selected bridge before importing a dashboard.

The top-level Grafana queries can continue to sum across slots. Across scrape
targets you need unique trusted ownership labels and each instance scraped once;
otherwise totals can double count, and slot 1 in different processes is not a
single owner. Within a process the slot is a local handle, not a project or user.
For troubleshooting, retain the topology plus slot in PromQL, for example:

```promql
sum by (job, instance, metric_slot, outcome) (
  rate(qaas_mocker_requests_completed_total{job="qaas_operational"}[5m])
)
```

Expected Mocker panels: accepted and outcome rates (per second), hit share
`hit/(hit+miss)`, internal-error share `error/accepted`, and p95 plus histogram
sample rate. Missing or zero denominators should remain No data. The ingest/queue
panels in the optional operations dashboard require a separate ingestion producer.

References: [OTel .NET custom metrics registration](https://opentelemetry.io/docs/zero-code/dotnet/custom/),
[OTel .NET exporters](https://opentelemetry.io/docs/languages/dotnet/exporters/),
[Collector Prometheus translation options](https://github.com/open-telemetry/opentelemetry-collector-contrib/blob/v0.165.0/exporter/prometheusexporter/README.md).

## Isolated bridge verification

The example [probe](probe) uses the real Mocker HTTP server and puts an optional
OpenTelemetry ASP.NET Core Prometheus exporter only in a separate test host.
The exporter is prerelease (1.21.0-beta.1) and is **not** a package dependency of
Mocker, a default scrape endpoint, or the preferred production bridge. The
[normalized readback](probe/verified-result.json) shows **30** Mocker series
for two concurrent enabled servers with differing outcomes, no series for a third
disabled server or in a clean all-disabled process, and identical HTTP status
and payloads with metrics on or off. It rejects duplicate and out-of-range
slots while an existing server is live, verifies that server still serves, and
restarts the process to prove the same slots start at zero. The verification
controls are routes on the separate test exporter, not product routes. For
the scrape result, only the stable exporter-added
`otel_scope_name` was removed; exporter target metadata is not part of the
Mocker metrics. The example Collector configuration is separately provided for
a host that chooses OTLP; the checked-in readback is from the direct test exporter.

From this directory on a Linux host that supports a disposable isolated network
namespace, build the opt-in example once, then run both cases on loopback:

```sh
cd probe
dotnet build Smoke.csproj
QAAS_PARENT_NET_NS="$(readlink /proc/self/ns/net)" \
  timeout 35s unshare --kill-child --user --map-root-user --net --fork python3 smoke.py
```
