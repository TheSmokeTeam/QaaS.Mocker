# Optional Mocker HTTP metrics

Set `EnableHttpMetrics: true` under an HTTP server (`Server.Http` or the corresponding
item under `Servers`). The default is `false`. The switch affects only HTTP/HTTPS
handlers for that server and does **not** add a route, open a metrics port, change
logging, or install/configure an exporter or observer. It does not emit from gRPC,
sockets, or requests rejected by Kestrel before this handler. A configured mock
readiness route still behaves like any other configured route; use a probe outside
this handler if you need a denominator excluding probe traffic.

The meter is `QaaS.Mocker.Http`. All opted-in HTTP servers within one process feed
one common aggregate. Disabled servers never update it. Counters reset when the
process restarts. A nonfatal observer failure during instrument publication
disables this meter for the process rather than preventing a server from starting.
Only the following values are exported to a consumer that elects
to listen:

| .NET instrument | Type | Expected Prometheus name with suffix translation | Semantics |
|---|---|---|---|
| `qaas.mocker.requests.accepted` | Cumulative counter | `qaas_mocker_requests_accepted_total` | Requests entering the opted-in handler |
| `qaas.mocker.requests.completed` | Cumulative counter | `qaas_mocker_requests_completed_total{outcome}` | Exactly one terminal handling outcome: `hit`, `miss`, or `error` |
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
explicit-bucket view. This is 15 application series per scrape target with those
buckets. Prometheus adds target topology; the host/exporter may itself publish
other series.

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
[normalized readback](probe/verified-result.json) shows 15 Mocker series after
five requests, zero Mocker series in a new disabled process, and identical HTTP
statuses in both. For the scrape result, only the stable exporter-added
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
