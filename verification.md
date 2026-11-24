# QaaS.Mocker observability verification

Pull request 45 was measured locally on Microsoft Windows 10 Home, 11th Gen Intel(R) Core(TM) i5-1135G7 @ 2.40GHz, 8 logical processors, 7.70 GiB RAM, .NET SDK 10.0.203, Node v24.16.0 and Python Python 3.14.5. The isolated services ran Elasticsearch 7.17.29 and Grafana 11.6.0. Time windows in the evidence are UTC.

The QaaS platform developer plugin was enabled at version 0.5.0. Its workflow skill matched the upstream revision `7aa8ad9e1de3f14d016c97929ccbc2f7fb7536c8`. The implementation used the reviewed plan and independent code and dashboard checks. Docker memory limits were 640 MiB for Elasticsearch and 240 MiB for Grafana.

| Build | Commit |
|---|---|
| Merge base | `363f4e22e209952db336558ca8aa0e31f10733b5` |
| Previous PR | `28edb44387b5ca000ca4072fdc5e5256b59c671b` |
| Reviewed source and dashboard | `707d875037018b080a38074208e4e1c562e7685a` |
| Paired product source | `5a7fea36db809f3c0ee0910a8ef146a4c5a7f7d8` |

The final CLI loaded `QaaS.Mocker.dll` with SHA-256 `988913b176fe5b55ea10a3ed786d8f6d94d59886394c0e4086e2d886d890285d`. Dashboard/config-only commits after the relevant runtime build are identified in the functional and performance bundles.

## Runtime and deployment

Each built Mocker run emits one start and one terminal through its existing Debug logger. Both use a product-generated attempt ID. Opted-in HTTP servers own six in-memory counter cells. A completion increments one cell and active servers publish a nonempty summary every ten seconds, plus a best-effort final summary. The CLI allows its existing sink up to 125 ms to drain after a successful startup connection. A host that never connected skips that wait. Existing HTTP responses, logging levels and diagnostic messages are preserved.

Configure the existing Elasticsearch sink or pass `--send-logs --elastic-uri <url>`. Supply trusted `MetaData.Team`, `MetaData.System` and string `MetaData.ExtraLabels.Project`. Install the native ingest processor and mappings on the current and future raw log indices, then start the product transform. Grafana uses index `qaasusage-mocker-v1`, time field `event.Time` and data source UID `qaas-mocker-usage-v1`. Enable `Server.Http.EnableHttpMetrics` on each HTTP server to collect the periodic counters. `QAAS_USAGE_FLUSH_MS` can set an explicit drain budget from 0 to 2000 ms. Launch the process with `QAAS_USAGE_ENABLED=false` to disable new usage records and keep ordinary logging. Restart a running service to apply the environment change. The transform can be stopped independently.

## What was verified

Runner made two witnessed requests to the real Mocker. With eight additional requests, Mocker served 10 total, with 1 handler error, 2 route misses and 2 HTTP 5xx. The configured 503 counted only toward 5xx. There were 3 starts, 2 finishes and 1 unsuccessful finish (50%). The intentionally killed process first appeared as overdue once the 15-minute grace period elapsed. An idle window emitted zero HTTP batches.

The final Runner Debug stream matched its merge base in content and order for all 85 ordinary lines on the passing fixture and 103 on the failing fixture, after normalizing clocks and generated attachment identifiers. Exit codes remained 0 and 1. Mocker retained its route-miss, fallback, exception and shutdown diagnostics in the real Kestrel workload.

All 1,110 Runner and 485 Mocker Release tests passed on the final runtime. One Runner discovery test missed its 120 ms response window on the first run under load. It passed in the complete isolated rerun. Grafana imported both dashboard JSON files and loaded both again from disk. The initial independent oracle compared 504 panel and scenario results to hand counts and event-level calculations. It distinguished 20 genuine CLI records, 11 exact redeliveries of genuine records, and 43 labeled fault records. The latter exercised partial counts, late/absent phases, unknown ownership, weighted counters and conflicting replays. The repeat against both final runtime binaries passed 504 additional panel comparisons across all seven dashboard filters, separately and in combination. Its receipt, filtered frame checks and binary hashes are in the archive.

## Performance

| Comparison | Workload | Elasticsearch | Pairs | Reference median | Candidate median | Paired overhead, 95% interval |
|---|---|---|---:|---:|---:|---|
| Merge base → candidate | http-mixed | healthy | 14 | 8,903.475 req/s | 7,607.996 req/s | +19.9% [-8.5%, +57.1%] |
| Merge base → candidate | http-mixed | slow | 4 | 10,861.379 req/s | 10,848.555 req/s | -4.6% [-61.7%, +137.3%] |
| Merge base → candidate | http-mixed | unreachable | 4 | 8,962.388 req/s | 9,677.868 req/s | -8.6% [-56.0%, +89.7%] |
| Merge base → candidate | mocker-short | slow | 3 | 1.488 s | 1.670 s | -24.8% [-88.3%, +385.5%] |
| Usage off → on | http-mixed | healthy | 4 | 2.508 ms p95 | 4.197 ms p95 | +35.8% [-61.4%, +378.0%] |
| Usage off → on | http-mixed | healthy | 4 | 11,126.083 req/s | 9,064.304 req/s | +22.2% [-37.6%, +139.4%] |
| Usage off → on | http-mixed | slow | 4 | 4,089.533 req/s | 8,926.423 req/s | -55.0% [-73.2%, -24.4%] |
| Usage off → on | http-mixed | unreachable | 4 | 9,325.253 req/s | 10,046.426 req/s | +14.1% [-50.1%, +161.0%] |
| Usage off → on | mocker-short | slow | 3 | 1.747 s | 1.940 s | +16.0% [-53.5%, +189.5%] |
| Previous PR → candidate | mocker-short | slow | 3 | 3.357 s | 1.921 s | -63.4% [-94.7%, +153.3%] |

Positive overhead means a slower operation or fewer requests per second. Timing intervals use the paired log ratio and apply separately to each comparison. Multiple comparisons were not adjusted. HTTP windows share warmed server processes. The complete observations include counts, byte sizes, CPU, working set, actual CLI exit and client-side tail latency. Healthy requests used local Elasticsearch. The slow relay delayed accepted bulk writes by one second, and the unavailable endpoint refused connections on loopback. WAN packet loss, DNS/TLS timeouts and multi-node production cluster load were outside these trials. This shared 8 GB machine cannot establish a universal 2% bound.

### Performance and delivery gate

The 2% ceiling remains unresolved for short CLI processes. On the final binary, six of six healthy short processes delivered their terminal record. With a one-second delay on accepted bulk writes, five of six delivered it, including two of three failing startups. In three paired normal short runs under delay, median exit was 1.747 s with usage off and 1.940 s with usage on. The paired overhead estimate was +16.0% with a 95% interval of −53.5% to +189.5%. The default connected shutdown budget is 125 ms. Operators can select zero wait when exit time takes priority or explicitly allow a longer drain when delivery takes priority.

Peak process memory exceeded 2% in measured healthy cases. Short normal runs added a median 11.5 MiB (+9.24%, 95% interval +7.44% to +11.07%). The warmed mixed HTTP workload added 4.41 MiB (+2.89%, interval +2.34% to +3.45%) within one matched host pair. The healthy HTTP workloads accepted no Elasticsearch documents with usage disabled. Enabling usage exercised the existing sink. The measurements cover the entire process and cannot assign those bytes to the counter array or a particular library.

The first four healthy mixed-load pairs against the merge base showed +73% elapsed time per request. Two independent process pairs with longer warmup gave +3.4% and +3.5% across six and four repeats, with wide intervals crossing zero and 2%. The report retains all three groups. The final healthy mixed workload counted all 49,639 requests in 2,617 compressed bulk-body bytes.

In a separate set of 15 matched short-process trials, the zero-wait setting delivered 0/15 terminal records and the 125 ms default delivered 15/15. Normal healthy peak working set fell from 138.9 to 126.3 MiB with zero wait. Those results explain the operational cost of selecting a shorter shutdown budget.

Timing the production drain callback directly gave 123 ms for a normal healthy exit and 133 ms for a normal exit with a delayed write. The latter includes thread dispatch and scheduling around the configured wait. A normal healthy callback added about 9.7 MiB to the process working set. Operator-visible process wall times and these narrower callback measurements are both in the archive.


## Operational limits

Counts cover observed telemetry from runs that entered execution. The transform projects an explicit field list. Ordinary logs retain their configured enrichment, and the dashboard reads organizational labels and generated attempt IDs. A long-running service or an abruptly stopped process may have no finish. Retain the raw indices through the maximum run and replay horizon. A late update after source expiry can lose the older phase during recomputation. The native transform has no built-in destination expiry in this configuration. Active and returning owners are approximate counts of known labels. Identical labels across teams collapse in the dimension-wide count. Use team filtering when the labels are only unique within a team. No eligible-team denominator is available. HTTP panels use publication time, so requests near a time boundary may appear in the next bucket. Empty windows and lost last batches are unobserved. The raw request summaries contain counters only. Request p95 is measured by the benchmark client. In RunLocally mode, the terminal measures command completion, which can precede listener shutdown. Before the final attribution change, flush-budget calibration delivered complete healthy pairs in 6/6 serial trials and 5/6 with competing startup load. An accepted bulk request can receive its acknowledgement after process exit.

The development binaries reported version `0.0.0`. The version filter was tested with this observed value. Release binaries report the assembly version supplied by their build.

Both HTTP time series have a ten-second minimum query interval. The browser sent `intervalMs=10000` in the short-range RPS query. The final ten requests landed as 3 and 7 by publication time, and the rendered points were 0.3 and 0.7 requests/s. [Grafana query options](https://grafana.com/docs/grafana/latest/visualizations/panels-visualizations/query-transform-data/)

## KPI reference

# Dashboard KPI definitions

The dashboard time zone is UTC. All panels honor team, project, system, environment, mode, version and feature filters. Start and finish timestamps are tested independently against the selected interval. An attempt crossing a boundary can contribute to one side. Canonical execution rows carry all observed phase times so Grafana’s outer time filter can retain either side.

## Measurement and reconciliation

The source is the existing application log stream indexed in Elasticsearch. The native ingest pipeline timestamps arrival. Continuous transforms reconcile attempts and HTTP batches, including duplicate and late deliveries. Refresh is scheduled every 30 seconds with a 10-second ingestion delay. Allow the transform and index refresh to finish before comparing.

Every Runner plan invocation and Mocker hosting attempt has its own `AttemptId`. One execution row pairs the earliest equivalent start and earliest equivalent finish. A repeated invocation has a distinct ID. Mocker batches use `ServerId` plus `BatchSequence`; a restart creates a fresh server ID. Each nonempty batch carries deltas. Idle intervals produce no sample. Repeated copies with different values, or disagreement between common immutable start/finish dimensions, mark a key conflicted. Operational panels exclude it. The conflict panel counts the affected keys.

The bins display the Framework environment and configured capabilities for that execution mode. A single attempt can appear in several capability bins. Components may be configured when a failure prevents later plan stages from running. Configuration supplies team/system labels and the explicit project label. Missing attribution is indexed under `(unattributed)`; literal trusted `Unknown` is a distinct label. Missing environment/version becomes `Unknown`, and an empty capability set becomes `none`. A filter narrows the population before aggregations, including owner cardinalities and rate denominators.

## Panels shared by both products

| Panel | Calculation within the selection |
|---|---|
| Runs started | Distinct canonical execution rows whose start timestamp is in range. |
| Runs completed | Distinct canonical execution rows whose terminal timestamp is in range. Includes successful and unsuccessful observed terminals. |
| Unsuccessful finishes | Sum of numeric `Failed` flags divided by observed terminal attempts. Cancellations count as unsuccessful finishes. |
| P95 run duration | Elasticsearch percentile estimate over measured terminal durations in milliseconds. Both products measure their lifecycle interval using a monotonic clock. |
| Starts by environment | Count by start-time histogram and Framework CI/local label. |
| Finished outcomes | Count by terminal-time histogram and outcome. Runner distinguishes assertion failure, broken assertion, cancellation and Runner error. Mocker distinguishes its hosting outcomes. |
| No observed finish after 15 min | Starts in range at least 15 minutes before the query end with no finish by that end. An active long-lived service also qualifies once it crosses the threshold. |
| Finish without observed start | In-range terminal records whose canonical row lacks a retained start. A retained start before the range still counts as present. |
| Completed runs / s | Number of in-range finishes divided by the full chosen interval in seconds, including idle time. |
| Unknown attribution | Starts with at least one missing trusted ownership dimension. |
| Conflicting replays | Number of canonical keys with incompatible copies or immutable phase dimensions; each conflicted key appears once. |
| Active teams, projects, systems | Per dimension, estimated distinct known labels among observed starts. These are counts of usage. An eligible-owner denominator would be required for an adoption percentage. |
| Returning teams, projects, systems | Estimated distinct known labels that occur in both equal-duration halves. The midpoint belongs to the second half. The query bounds the overlap estimate between zero and the smaller group. A half without observable owners makes the comparison unavailable. |
| Usage by team, project and system | Starts grouped by up to five teams, five projects per team and five systems per project, ordered by volume. |
| Usage by version and mode | Starts grouped by up to eight versions and eight modes per version. |
| Feature use | Starts per configured capability and time bucket. Adding the series can count one execution multiple times. |
| Outcomes by team and system | Up to eight teams and eight systems per team, with finishes, unsuccessful count and unsuccessful fraction. |
| Recent unsuccessful executions | Up to 25 unsuccessful terminals with finish time, trusted ownership, execution mode, outcome, monotonic duration and the opaque attempt ID. |

The owner popularity metrics use Elasticsearch cardinality with precision threshold 4,000. Low-cardinality fixtures have exact hand-counted comparisons here. Partial or missing records affect observed counts. An absent group can render a dash, and an actual measured zero remains a zero.

## Runner panels

| Panel | Unit and timing |
|---|---|
| Test plans executed | Sum of full `Run` attempts entered, attributed to finish time. An expanded case is one plan. Split `Act` and `Assert` phases contribute zero plans. |
| Assertions executed | Sum of completed assertion results for finishes with an exact count. Reusing an execution object counts only the new results for the current attempt. |
| Assertions / s | Sum of observed completed assertions divided by the range duration in seconds. |
| Unmeasured assertion counts | Number of finishes missing an exact count. An interrupted parallel batch can leave results unpublished. |
| Mode, case and batch | Finishes, full plans and observed completed assertions grouped by `Run`, `Act` or `Assert` and standalone, named case, batch command or batch case. The assertion total is absent when every finish in the group lacks a count. |

Runner emits lifecycle records only after a measured execution enters its plan. Parsing or configuration failures before that point, help and template generation do not produce usage pairs. Assertion hooks may perform their own checks internally. The counted unit is the completed Runner assertion result.

## Mocker panels

| Panel | Unit and timing |
|---|---|
| Completed requests | Sum of `Requests` deltas across nonconflicting HTTP batches published within the range. |
| Handler error rate | Sum of `HandlerErrors` divided by summed requests. This includes recovered processing/response failures. |
| Route miss rate | Sum of `Misses` divided by summed requests. Includes route/method misses handled by the not-found path. |
| HTTP 5xx rate | Sum of actual 500–599 responses divided by summed requests, including intentionally mocked 5xx responses. |
| HTTP requests / s | Requests in each flush-time histogram bucket divided by that bucket’s duration in seconds. |
| HTTP outcomes over time | Separate sums per flush-time bucket of at least 10 seconds for handler errors, route misses and 5xx. The same response can contribute to more than one series. |
| Busiest HTTP server instances | Up to 20 distinct server lifetimes ranked by summed requests, with port and each failure counter. |

Ratios use summed numerators and denominators across batches; a small flush carries proportionally less weight. Requests that never reach a recorded completion or a published batch are unobserved. The fault inputs exercise unequal batch sizes and a conflicting batch; the genuine workload independently witnesses the two cross-project responses.


## Full benchmark tables

# Mocker release performance verification

PR source head 707d875037018b080a38074208e4e1c562e7685a. Measured runtime SHA-256 988913b176fe5b55ea10a3ed786d8f6d94d59886394c0e4086e2d886d890285d. Merge base 363f4e22e209952db336558ca8aa0e31f10733b5. Documentation, Elasticsearch and Grafana settings were committed after the identical runtime was built.

318 observations in 159 matched pairs across 41 workload and comparison settings. Absolute values show within-arm medians. Percentage change compares aligned alternating arms. Ratios use positive values for slower operation. Confidence limits are paired two-sided 95% intervals on log ratios. Absolute differences are available for metrics with a zero baseline.

The candidate-usage comparison toggles QAAS_USAGE_ENABLED on the same assembly with identical sink settings. Base-head uses the merge base. Previous-head uses the prior published PR revision. Same-controls launches exactly the same binary and configuration in both arms. Flush-zero compares an explicit zero wait with the default, with usage enabled on both arms. Explicit flush-choice compares the default with the separately configured 2,000 ms shutdown allowance.


## Comparison with the 2% target

These counts use the same final binaries with global usage off and on under otherwise matched logging. The throughput row counts one main statistic per workload and condition. Positive throughput overhead means more elapsed time for an equal number of requests. HTTP batches within a long-running host share that host, so its peak-working-set observations are not independent machine samples.

| Measure | Conditions | Entire 95% interval ≤ +2% | Entire interval > +2% | Crosses target |
|---|---:|---:|---:|---:|
| Process CPU seconds per CLI | 12 | 0 | 0 | 12 |
| CLI or request throughput | 20 | 3 | 0 | 17 |
| Request p95 | 8 | 3 | 0 | 5 |
| CPU per request | 8 | 1 | 0 | 7 |
| Peak process working set | 20 | 4 | 7 | 9 |

Conditions with the full interval above +2% follow. The raw observations retain each original run, including any follow-up pairs.

- http-hit, slow peak working set: +4.41% (+3.75% to +5.07%, 4 pairs).
- http-mixed, healthy peak working set: +2.89% (+2.34% to +3.45%, 4 pairs).
- http-mixed, slow peak working set: +2.84% (+2.24% to +3.45%, 4 pairs).
- mocker-literal-unknown, healthy peak working set: +8.80% (+7.39% to +10.22%, 9 pairs).
- mocker-short, healthy peak working set: +9.24% (+7.44% to +11.07%, 3 pairs).
- mocker-short, slow peak working set: +7.81% (+4.18% to +11.56%, 3 pairs).
- mocker-unowned, healthy peak working set: +9.64% (+7.07% to +12.27%, 3 pairs).

## Workload timing and process resources

| Comparison | Workload | Elastic | n | Off / base | On / current | Overhead and 95% interval | CPU off/on (s) | Peak working set off/on (MiB) |
|---|---|---|---:|---:|---:|---|---:|---:|
| base-head | http-hit | healthy | 4 | 7,605.370 req/s | 8,100.193 req/s | +2.06% (-24.78% to +38.49%) | 2.60/2.81 | 147.48/149.99 |
| base-head | http-hit | healthy | 4 | 4.842 ms p95 | 4.429 ms p95 | +3.83% (-65.74% to +214.69%) | 2.60/2.81 | 147.48/149.99 |
| base-head | http-hit | slow | 4 | 9,536.328 req/s | 7,101.057 req/s | +24.71% (-37.96% to +150.71%) | 2.71/2.67 | 146.36/148.89 |
| base-head | http-hit | slow | 4 | 3.611 ms p95 | 4.949 ms p95 | +40.45% (-70.57% to +570.28%) | 2.71/2.67 | 146.36/148.89 |
| base-head | http-hit | unreachable | 4 | 8,823.801 req/s | 13,098.610 req/s | -32.74% (-74.00% to +74.02%) | 2.61/2.85 | 143.04/145.09 |
| base-head | http-hit | unreachable | 4 | 5.187 ms p95 | 2.128 ms p95 | -45.18% (-93.12% to +337.07%) | 2.61/2.85 | 143.04/145.09 |
| base-head | http-mixed | healthy | 14 | 8,903.475 req/s | 7,607.996 req/s | +19.87% (-8.54% to +57.10%) | 2.80/2.95 | 148.50/151.83 |
| base-head | http-mixed | healthy | 14 | 3.662 ms p95 | 4.764 ms p95 | +45.35% (-13.28% to +143.61%) | 2.80/2.95 | 148.50/151.83 |
| base-head | http-mixed | slow | 4 | 10,861.379 req/s | 10,848.555 req/s | -4.60% (-61.65% to +137.32%) | 3.27/3.90 | 148.23/149.61 |
| base-head | http-mixed | slow | 4 | 3.368 ms p95 | 3.230 ms p95 | -12.50% (-85.69% to +435.09%) | 3.27/3.90 | 148.23/149.61 |
| base-head | http-mixed | unreachable | 4 | 8,962.388 req/s | 9,677.868 req/s | -8.65% (-56.01% to +89.69%) | 3.19/3.49 | 145.57/147.57 |
| base-head | http-mixed | unreachable | 4 | 3.956 ms p95 | 3.722 ms p95 | +1.54% (-78.17% to +372.38%) | 3.19/3.49 | 145.57/147.57 |
| base-head | mocker-short | slow | 3 | 1.488 s | 1.670 s | -24.77% (-88.34% to +385.51%) | 1.56/1.75 | 125.95/136.18 |
| base-head | mocker-start-fail | slow | 3 | 1.895 s | 2.039 s | -0.05% (-27.20% to +37.21%) | 2.00/1.91 | 137.47/138.86 |
| candidate-usage | http-hit | disabled | 4 | 10,968.518 req/s | 10,583.195 req/s | +12.82% (-46.61% to +138.40%) | 2.12/2.12 | 136.04/136.06 |
| candidate-usage | http-hit | disabled | 4 | 2.935 ms p95 | 2.764 ms p95 | +12.43% (-75.42% to +414.18%) | 2.12/2.12 | 136.04/136.06 |
| candidate-usage | http-hit | healthy | 4 | 6,408.572 req/s | 11,503.795 req/s | -42.48% (-61.59% to -13.87%) | 2.45/2.68 | 146.21/147.59 |
| candidate-usage | http-hit | healthy | 4 | 6.754 ms p95 | 2.108 ms p95 | -70.73% (-82.34% to -51.49%) | 2.45/2.68 | 146.21/147.59 |
| candidate-usage | http-hit | slow | 4 | 8,046.406 req/s | 11,877.690 req/s | +1.74% (-79.87% to +414.18%) | 2.47/2.23 | 144.54/150.54 |
| candidate-usage | http-hit | slow | 4 | 4.764 ms p95 | 2.044 ms p95 | -26.51% (-94.42% to +868.22%) | 2.47/2.23 | 144.54/150.54 |
| candidate-usage | http-hit | unreachable | 4 | 4,771.838 req/s | 9,002.068 req/s | -48.27% (-68.85% to -14.09%) | 1.59/2.64 | 142.98/143.89 |
| candidate-usage | http-hit | unreachable | 4 | 9.873 ms p95 | 2.949 ms p95 | -68.90% (-88.08% to -18.81%) | 1.59/2.64 | 142.98/143.89 |
| candidate-usage | http-mixed | disabled | 4 | 7,602.403 req/s | 11,558.361 req/s | -25.56% (-79.29% to +167.57%) | 2.49/2.53 | 138.53/138.26 |
| candidate-usage | http-mixed | disabled | 4 | 3.779 ms p95 | 2.365 ms p95 | -27.15% (-90.89% to +482.68%) | 2.49/2.53 | 138.53/138.26 |
| candidate-usage | http-mixed | healthy | 4 | 11,126.083 req/s | 9,064.304 req/s | +22.23% (-37.60% to +139.44%) | 2.31/2.37 | 148.06/152.04 |
| candidate-usage | http-mixed | healthy | 4 | 2.508 ms p95 | 4.197 ms p95 | +35.75% (-61.45% to +378.01%) | 2.31/2.37 | 148.06/152.04 |
| candidate-usage | http-mixed | slow | 4 | 4,089.533 req/s | 8,926.423 req/s | -55.01% (-73.22% to -24.42%) | 2.83/3.24 | 146.76/150.62 |
| candidate-usage | http-mixed | slow | 4 | 11.851 ms p95 | 2.778 ms p95 | -72.95% (-88.36% to -37.14%) | 2.83/3.24 | 146.76/150.62 |
| candidate-usage | http-mixed | unreachable | 4 | 9,325.253 req/s | 10,046.426 req/s | +14.11% (-50.11% to +161.01%) | 2.33/2.58 | 145.10/146.15 |
| candidate-usage | http-mixed | unreachable | 4 | 3.757 ms p95 | 2.836 ms p95 | +5.18% (-81.04% to +483.43%) | 2.33/2.58 | 145.10/146.15 |
| candidate-usage | mocker-build-rejection | healthy | 3 | 1.534 s | 1.391 s | -3.44% (-25.47% to +25.12%) | 1.33/1.39 | 117.75/118.31 |
| candidate-usage | mocker-literal-unknown | healthy | 9 | 1.421 s | 1.904 s | +18.68% (-1.76% to +43.38%) | 1.45/1.92 | 126.56/138.32 |
| candidate-usage | mocker-short | disabled | 3 | 1.306 s | 1.108 s | -14.70% (-56.06% to +65.63%) | 1.30/1.11 | 106.60/106.96 |
| candidate-usage | mocker-short | healthy | 3 | 1.774 s | 2.020 s | +13.22% (-44.70% to +131.80%) | 1.80/2.11 | 126.54/138.10 |
| candidate-usage | mocker-short | slow | 3 | 1.747 s | 1.940 s | +16.03% (-53.50% to +189.52%) | 1.78/1.97 | 126.21/137.02 |
| candidate-usage | mocker-short | unreachable | 3 | 5.964 s | 5.267 s | -3.19% (-29.37% to +32.69%) | 1.98/1.25 | 115.02/114.90 |
| candidate-usage | mocker-start-fail | disabled | 3 | 1.549 s | 1.237 s | -24.45% (-65.98% to +67.77%) | 1.62/1.23 | 115.78/114.27 |
| candidate-usage | mocker-start-fail | healthy | 3 | 1.937 s | 2.089 s | +12.85% (-55.26% to +184.66%) | 1.89/2.05 | 136.37/139.68 |
| candidate-usage | mocker-start-fail | slow | 3 | 2.239 s | 2.004 s | -3.61% (-46.08% to +72.29%) | 1.89/1.88 | 135.67/138.47 |
| candidate-usage | mocker-start-fail | unreachable | 3 | 5.675 s | 5.679 s | -1.33% (-21.59% to +24.18%) | 1.66/1.73 | 123.11/124.43 |
| candidate-usage | mocker-template | healthy | 3 | 1.363 s | 1.872 s | +36.71% (-13.22% to +115.36%) | 1.34/1.89 | 125.82/126.19 |
| candidate-usage | mocker-unowned | healthy | 3 | 1.428 s | 1.986 s | +22.35% (-45.92% to +176.83%) | 1.41/2.08 | 125.75/137.92 |
| flush-choice | mocker-short | healthy | 3 | 3.333 s | 2.723 s | -24.36% (-36.56% to -9.82%) | 2.88/2.42 | 136.21/139.18 |
| flush-choice | mocker-short | slow | 3 | 3.316 s | 3.917 s | +54.14% (-48.69% to +363.05%) | 2.64/3.70 | 133.36/140.76 |
| flush-choice | mocker-start-fail | healthy | 3 | 2.973 s | 3.339 s | +11.56% (-17.48% to +50.81%) | 2.80/3.14 | 138.76/140.30 |
| flush-choice | mocker-start-fail | slow | 3 | 3.491 s | 3.369 s | +1.81% (-53.24% to +121.71%) | 3.08/2.52 | 136.64/143.81 |
| flush-zero | mocker-literal-unknown | healthy | 3 | 1.544 s | 1.665 s | +10.84% (-20.78% to +55.09%) | 1.52/1.81 | 126.05/136.16 |
| flush-zero | mocker-short | healthy | 3 | 1.815 s | 1.795 s | +4.95% (-55.26% to +146.22%) | 1.61/1.95 | 126.34/138.89 |
| flush-zero | mocker-short | slow | 3 | 2.143 s | 1.903 s | -0.26% (-45.86% to +83.74%) | 2.05/1.92 | 127.17/137.30 |
| flush-zero | mocker-start-fail | healthy | 3 | 1.970 s | 1.585 s | -8.58% (-62.51% to +122.94%) | 2.02/1.50 | 137.34/140.12 |
| flush-zero | mocker-start-fail | slow | 3 | 1.499 s | 1.663 s | +8.20% (-22.86% to +51.76%) | 1.53/1.55 | 137.75/138.72 |
| previous-head | mocker-short | slow | 3 | 3.357 s | 1.921 s | -63.38% (-94.71% to +153.26%) | 2.94/1.98 | 142.43/137.33 |
| previous-head | mocker-start-fail | slow | 3 | 2.961 s | 1.748 s | -47.67% (-73.98% to +5.25%) | 2.39/1.55 | 142.98/138.60 |
| same-controls | http-hit | healthy | 8 | 7,321.931 req/s | 8,251.237 req/s | -8.85% (-31.62% to +21.50%) | 2.39/2.28 | 146.34/146.75 |
| same-controls | http-hit | healthy | 8 | 3.983 ms p95 | 5.382 ms p95 | +10.58% (-42.17% to +111.44%) | 2.39/2.28 | 146.34/146.75 |
| same-controls | mocker-short | healthy | 4 | 3.027 s | 2.158 s | -24.20% (-48.19% to +10.91%) | 2.68/1.84 | 126.46/126.10 |

## Elasticsearch ingestion

CLI rows below aggregate the actual acknowledgements across independent processes. A delayed bulk request can finish after its process exits. Each relay stays alive outside the timed window to record that acknowledgement. Closed/disabled destinations accept zero documents. The usage-byte column counts compact uncompressed serialized usage event bodies. Total wire bytes also include ordinary diagnostic documents and bulk metadata.

| Comparison | Workload | Elastic | Arm | Runs | Accepted usage | Accepted diagnostics | Usage document bytes | All bulk wire bytes |
|---|---|---|---|---:|---:|---:|---:|---:|
| base-head | mocker-short | slow | off | 3 | 0 | 0 | 0 | 0 |
| base-head | mocker-short | slow | on | 3 | 4 | 0 | 4692 | 1302 |
| base-head | mocker-start-fail | slow | off | 3 | 0 | 0 | 0 | 0 |
| base-head | mocker-start-fail | slow | on | 3 | 6 | 0 | 7108 | 1972 |
| candidate-usage | mocker-build-rejection | healthy | off | 3 | 0 | 0 | 0 | 0 |
| candidate-usage | mocker-build-rejection | healthy | on | 3 | 0 | 0 | 0 | 0 |
| candidate-usage | mocker-literal-unknown | healthy | off | 9 | 0 | 0 | 0 | 0 |
| candidate-usage | mocker-literal-unknown | healthy | on | 9 | 18 | 0 | 20680 | 5732 |
| candidate-usage | mocker-short | disabled | off | 3 | 0 | 0 | 0 | 0 |
| candidate-usage | mocker-short | disabled | on | 3 | 0 | 0 | 0 | 0 |
| candidate-usage | mocker-short | healthy | off | 3 | 0 | 0 | 0 | 0 |
| candidate-usage | mocker-short | healthy | on | 3 | 6 | 0 | 7038 | 1952 |
| candidate-usage | mocker-short | slow | off | 3 | 0 | 0 | 0 | 0 |
| candidate-usage | mocker-short | slow | on | 3 | 6 | 0 | 7038 | 1952 |
| candidate-usage | mocker-short | unreachable | off | 3 | 0 | 0 | 0 | 0 |
| candidate-usage | mocker-short | unreachable | on | 3 | 0 | 0 | 0 | 0 |
| candidate-usage | mocker-start-fail | disabled | off | 3 | 0 | 0 | 0 | 0 |
| candidate-usage | mocker-start-fail | disabled | on | 3 | 0 | 0 | 0 | 0 |
| candidate-usage | mocker-start-fail | healthy | off | 3 | 0 | 0 | 0 | 0 |
| candidate-usage | mocker-start-fail | healthy | on | 3 | 6 | 0 | 7106 | 1967 |
| candidate-usage | mocker-start-fail | slow | off | 3 | 0 | 0 | 0 | 0 |
| candidate-usage | mocker-start-fail | slow | on | 3 | 4 | 0 | 4736 | 1310 |
| candidate-usage | mocker-start-fail | unreachable | off | 3 | 0 | 0 | 0 | 0 |
| candidate-usage | mocker-start-fail | unreachable | on | 3 | 0 | 0 | 0 | 0 |
| candidate-usage | mocker-template | healthy | off | 3 | 0 | 0 | 0 | 0 |
| candidate-usage | mocker-template | healthy | on | 3 | 0 | 0 | 0 | 0 |
| candidate-usage | mocker-unowned | healthy | off | 3 | 0 | 0 | 0 | 0 |
| candidate-usage | mocker-unowned | healthy | on | 3 | 6 | 0 | 6846 | 1830 |
| flush-choice | mocker-short | healthy | off | 3 | 6 | 0 | 7038 | 1950 |
| flush-choice | mocker-short | healthy | on | 3 | 6 | 0 | 7040 | 1954 |
| flush-choice | mocker-short | slow | off | 3 | 4 | 0 | 4690 | 1300 |
| flush-choice | mocker-short | slow | on | 3 | 6 | 0 | 7042 | 1952 |
| flush-choice | mocker-start-fail | healthy | off | 3 | 5 | 0 | 5912 | 1896 |
| flush-choice | mocker-start-fail | healthy | on | 3 | 6 | 0 | 7104 | 1970 |
| flush-choice | mocker-start-fail | slow | off | 3 | 6 | 0 | 7108 | 1970 |
| flush-choice | mocker-start-fail | slow | on | 3 | 6 | 0 | 7104 | 1971 |
| flush-zero | mocker-literal-unknown | healthy | off | 3 | 0 | 0 | 0 | 0 |
| flush-zero | mocker-literal-unknown | healthy | on | 3 | 6 | 0 | 6894 | 1910 |
| flush-zero | mocker-short | healthy | off | 3 | 0 | 0 | 0 | 0 |
| flush-zero | mocker-short | healthy | on | 3 | 6 | 0 | 7038 | 1949 |
| flush-zero | mocker-short | slow | off | 3 | 0 | 0 | 0 | 0 |
| flush-zero | mocker-short | slow | on | 3 | 6 | 0 | 7038 | 1952 |
| flush-zero | mocker-start-fail | healthy | off | 3 | 0 | 0 | 0 | 0 |
| flush-zero | mocker-start-fail | healthy | on | 3 | 6 | 0 | 7104 | 1968 |
| flush-zero | mocker-start-fail | slow | off | 3 | 0 | 0 | 0 | 0 |
| flush-zero | mocker-start-fail | slow | on | 3 | 6 | 0 | 7104 | 1967 |
| previous-head | mocker-short | slow | off | 3 | 6 | 0 | 3486 | 1234 |
| previous-head | mocker-short | slow | on | 3 | 6 | 0 | 7038 | 1951 |
| previous-head | mocker-start-fail | slow | off | 3 | 6 | 0 | 3486 | 1241 |
| previous-head | mocker-start-fail | slow | on | 3 | 6 | 0 | 7110 | 1970 |
| same-controls | mocker-short | healthy | off | 4 | 0 | 0 | 0 | 0 |
| same-controls | mocker-short | healthy | on | 4 | 0 | 0 | 0 | 0 |

## HTTP outcome receipts

The expected totals below include all timed and warmup requests and the single readiness GET. Handler errors are the 500 responses from the throwing processor. The 5xx column additionally includes intentional 503 responses. Completed requests can legitimately be absent when shipping is disabled or refused.

| Comparison | Workload | Sink | Arm | Requests sent / accepted | Handler 500 / counted | Misses / counted | Total 5xx / counted | Usage docs | Usage source bytes | All bulk wire bytes |
|---|---|---|---|---:|---:|---:|---:|---:|---:|---:|
| candidate-usage | http-hit | healthy | off | 33,447 / 0 | 0 / 0 | 0 / 0 | 0 / 0 | 0 | 0 | 0 |
| candidate-usage | http-hit | healthy | on | 56,628 / 56,628 | 0 / 0 | 0 / 0 | 0 / 0 | 3 | 3848 | 1884 |
| candidate-usage | http-mixed | healthy | off | 58,820 / 0 | 14,703 / 0 | 14,706 / 0 | 29,407 / 0 | 0 | 0 | 0 |
| candidate-usage | http-mixed | healthy | on | 49,639 / 49,639 | 12,408 / 12,408 | 12,410 / 12,410 | 24,816 / 24,816 | 4 | 5346 | 2617 |
| candidate-usage | http-hit | slow | off | 40,743 / 0 | 0 / 0 | 0 / 0 | 0 / 0 | 0 | 0 | 0 |
| candidate-usage | http-hit | slow | on | 49,353 / 49,353 | 0 / 0 | 0 / 0 | 0 / 0 | 3 | 3848 | 1892 |
| candidate-usage | http-mixed | slow | off | 23,370 / 0 | 5,841 / 0 | 5,842 / 0 | 11,682 / 0 | 0 | 0 | 0 |
| candidate-usage | http-mixed | slow | on | 47,630 / 47,630 | 11,905 / 11,905 | 11,908 / 11,908 | 23,812 / 23,812 | 3 | 3872 | 1905 |
| candidate-usage | http-hit | unreachable | off | 26,181 / 0 | 0 / 0 | 0 / 0 | 0 / 0 | 0 | 0 | 0 |
| candidate-usage | http-hit | unreachable | on | 46,279 / 0 | 0 / 0 | 0 / 0 | 0 / 0 | 0 | 0 | 0 |
| candidate-usage | http-mixed | unreachable | off | 48,528 / 0 | 12,131 / 0 | 12,132 / 0 | 24,262 / 0 | 0 | 0 | 0 |
| candidate-usage | http-mixed | unreachable | on | 46,650 / 0 | 11,661 / 0 | 11,663 / 0 | 23,323 / 0 | 0 | 0 | 0 |
| candidate-usage | http-hit | disabled | off | 53,217 / 0 | 0 / 0 | 0 / 0 | 0 / 0 | 0 | 0 | 0 |
| candidate-usage | http-hit | disabled | on | 49,930 / 0 | 0 / 0 | 0 / 0 | 0 / 0 | 0 | 0 | 0 |
| candidate-usage | http-mixed | disabled | off | 39,168 / 0 | 9,791 / 0 | 9,792 / 0 | 19,582 / 0 | 0 | 0 | 0 |
| candidate-usage | http-mixed | disabled | on | 51,978 / 0 | 12,992 / 0 | 12,995 / 0 | 25,986 / 0 | 0 | 0 | 0 |
| base-head | http-hit | healthy | off | 45,723 / 0 | 0 / 0 | 0 / 0 | 0 / 0 | 0 | 0 | 0 |
| base-head | http-hit | healthy | on | 46,075 / 46,075 | 0 / 0 | 0 / 0 | 0 / 0 | 4 | 5312 | 2587 |
| base-head | http-mixed | healthy | off | 56,254 / 0 | 14,062 / 0 | 14,064 / 0 | 28,124 / 0 | 0 | 0 | 0 |
| base-head | http-mixed | healthy | on | 34,448 / 34,448 | 8,609 / 8,609 | 8,613 / 8,613 | 17,221 / 17,221 | 4 | 5350 | 2619 |
| base-head | http-hit | slow | off | 60,503 / 0 | 0 / 0 | 0 / 0 | 0 / 0 | 0 | 0 | 0 |
| base-head | http-hit | slow | on | 52,149 / 52,149 | 0 / 0 | 0 / 0 | 0 / 0 | 4 | 5310 | 2592 |
| base-head | http-mixed | slow | off | 62,662 / 0 | 15,663 / 0 | 15,666 / 0 | 31,328 / 0 | 0 | 0 | 0 |
| base-head | http-mixed | slow | on | 63,812 / 63,812 | 15,951 / 15,951 | 15,953 / 15,953 | 31,904 / 31,904 | 4 | 5354 | 2625 |
| base-head | http-hit | unreachable | off | 61,253 / 0 | 0 / 0 | 0 / 0 | 0 / 0 | 0 | 0 | 0 |
| base-head | http-hit | unreachable | on | 82,901 / 0 | 0 / 0 | 0 / 0 | 0 / 0 | 0 | 0 | 0 |
| base-head | http-mixed | unreachable | off | 54,108 / 0 | 13,525 / 0 | 13,528 / 0 | 27,051 / 0 | 0 | 0 | 0 |
| base-head | http-mixed | unreachable | on | 57,957 / 0 | 14,487 / 0 | 14,489 / 0 | 28,976 / 0 | 0 | 0 | 0 |
| same-controls | http-hit | healthy | off | 96,823 / 0 | 0 / 0 | 0 / 0 | 0 / 0 | 0 | 0 | 0 |
| same-controls | http-hit | healthy | on | 101,354 / 0 | 0 / 0 | 0 / 0 | 0 / 0 | 0 | 0 | 0 |
| base-head | http-mixed | healthy | on | 81,833 / 81,833 | 20,456 / 20,456 | 20,458 / 20,458 | 40,913 / 40,913 | 5 | 6834 | 3335 |
| base-head | http-mixed | healthy | off | 84,663 / 0 | 21,163 / 0 | 21,167 / 0 | 42,327 / 0 | 0 | 0 | 0 |
| base-head | http-mixed | healthy | off | 83,260 / 0 | 20,813 / 0 | 20,815 / 0 | 41,627 / 0 | 0 | 0 | 0 |
| base-head | http-mixed | healthy | on | 79,103 / 79,103 | 19,773 / 19,773 | 19,777 / 19,777 | 39,548 / 39,548 | 4 | 5356 | 2619 |

The HTTP delivery file includes the exact number of client requests, accepted batch totals, encoded bytes, final shutdown method and memory high-water mark for each host. Readiness/warmup traffic is included there and excluded from timed throughput.

## Short CLI delivery by shutdown allowance

Each paired series sets the same usage switch and logging configuration in both arms. A full receipt is an acknowledged start and terminal in the same process. The default wait is 125 ms on a connected sink.

| Paired series | Sink | Workload | Shutdown allowance | CLI median (s) | Peak working set median (MiB) | Full receipts / processes |
|---|---|---|---|---:|---:|---:|
| flush-choice | healthy | mocker-short | 125 ms (default) | 3.333 | 136.21 | 3/3 |
| flush-choice | healthy | mocker-short | 2,000 ms | 2.723 | 139.18 | 3/3 |
| flush-choice | slow | mocker-short | 125 ms (default) | 3.316 | 133.36 | 2/3 |
| flush-choice | slow | mocker-short | 2,000 ms | 3.917 | 140.76 | 3/3 |
| flush-choice | healthy | mocker-start-fail | 125 ms (default) | 2.973 | 138.76 | 2/3 |
| flush-choice | healthy | mocker-start-fail | 2,000 ms | 3.339 | 140.30 | 3/3 |
| flush-choice | slow | mocker-start-fail | 125 ms (default) | 3.491 | 136.64 | 3/3 |
| flush-choice | slow | mocker-start-fail | 2,000 ms | 3.369 | 143.81 | 3/3 |
| flush-zero | healthy | mocker-literal-unknown | 0 ms | 1.544 | 126.05 | 0/3 |
| flush-zero | healthy | mocker-literal-unknown | 125 ms (default) | 1.665 | 136.16 | 3/3 |
| flush-zero | healthy | mocker-short | 0 ms | 1.815 | 126.34 | 0/3 |
| flush-zero | healthy | mocker-short | 125 ms (default) | 1.795 | 138.89 | 3/3 |
| flush-zero | slow | mocker-short | 0 ms | 2.143 | 127.17 | 0/3 |
| flush-zero | slow | mocker-short | 125 ms (default) | 1.903 | 137.30 | 3/3 |
| flush-zero | healthy | mocker-start-fail | 0 ms | 1.970 | 137.34 | 0/3 |
| flush-zero | healthy | mocker-start-fail | 125 ms (default) | 1.585 | 140.12 | 3/3 |
| flush-zero | slow | mocker-start-fail | 0 ms | 1.499 | 137.75 | 0/3 |
| flush-zero | slow | mocker-start-fail | 125 ms (default) | 1.663 | 138.72 | 3/3 |

## Repeated healthy short-run signal

The trusted owner named Unknown is eligible for measurement. The same-input global usage switch changes whether this short process emits records and may enter its normal shutdown wait. The first series and independently launched follow-up pairs are kept together in the main table.

| Observations | Pairs | Off median (s) | On median (s) | Paired change and 95% interval |
|---|---:|---:|---:|---:|
| Initial | 3 | 1.421 | 1.950 | +33.25% (+25.07% to +41.97%) |
| Confirmation | 6 | 1.459 | 1.736 | +12.01% (-17.44% to +51.96%) |
| All | 9 | 1.421 | 1.904 | +18.68% (-1.76% to +43.38%) |

## Independent follow-up pairs

The healthy workloads below triggered follow-up runs when the initial interval lay above +2%. The mixed HTTP reruns used new Kestrel processes, a longer warmup, and a reversed port and startup order for the first follow-up. Positive indicates higher elapsed time per operation.

| Workload and metric | Initial pairs | Follow-up pairs | Combined estimate |
|---|---:|---:|---:|
| http-mixed, rps | +73.28% (+5.07% to +185.77%, n=4) | +3.44% (-24.86% to +42.40%, n=10) | +19.87% (-8.54% to +57.10%, n=14) |
| http-mixed, p95_ms | +157.68% (+4.90% to +532.93%, n=4) | +15.60% (-40.12% to +123.15%, n=10) | +45.35% (-13.28% to +143.61%, n=14) |

## Compiled HTTP handler

The exact server handler ran with fresh DefaultHttpContext instances and its production counter enabled and disabled on one warmed process. Each paired round sums 32 alternating adjacent blocks, and every status code was verified.

| Route | Writers | Pairs | Change | 95% interval |
|---|---:|---:|---:|---:|
| /ok | 1 | 16 | +2.10% | -2.02% to +6.40% |
| /ok | 8 | 16 | -1.34% | -11.02% to +9.38% |
| /missing | 4 | 16 | +3.18% | -5.85% to +13.07% |
| /degraded | 4 | 16 | +7.21% | -4.50% to +20.34% |
| /throw | 4 | 16 | -1.38% | -16.54% to +16.54% |

11,673,600 status-checked completions. [Raw handler observations](mocker-handler.json)

## Shutdown callback

A diagnostic wrapper timed the original synchronous CLI drain callback on the compiled hosts. It records whether the real callback was attached and invoked and the time spent inside it; these separate profile launches do not enter the primary CLI comparison.

| Sink | Workload | Build | Callback invocations | Median contribution (ms) | Working set after bootstrap (MiB) | Working set added in callback (MiB) |
|---|---|---|---:|---:|---:|---:|
| healthy | mocker-literal-unknown | base | 0/3 | 0.00 | 118.62 | — |
| healthy | mocker-literal-unknown | head | 3/3 | 130.72 | 118.81 | 11.18 |
| healthy | mocker-literal-unknown | head-off | 0/3 | 0.00 | 118.69 | — |
| healthy | mocker-short | base | 0/3 | 0.00 | 118.69 | — |
| healthy | mocker-short | head | 3/3 | 123.45 | 118.69 | 9.70 |
| healthy | mocker-short | head-off | 0/3 | 0.00 | 118.73 | — |
| healthy | mocker-start-fail | base | 0/3 | 0.00 | 118.58 | — |
| healthy | mocker-start-fail | head | 3/3 | 96.05 | 118.75 | 2.94 |
| healthy | mocker-start-fail | head-off | 0/3 | 0.00 | 118.61 | — |
| slow | mocker-short | base | 0/3 | 0.00 | 118.67 | — |
| slow | mocker-short | head | 3/3 | 132.73 | 118.71 | 6.34 |
| slow | mocker-start-fail | base | 0/3 | 0.00 | 118.52 | — |
| slow | mocker-start-fail | head | 3/3 | 126.94 | 118.68 | 1.38 |
| unreachable | mocker-short | base | 0/3 | 0.00 | 108.79 | — |
| unreachable | mocker-short | head | 0/3 | 0.00 | 108.83 | — |
| unreachable | mocker-start-fail | base | 0/3 | 0.00 | 108.13 | — |
| unreachable | mocker-start-fail | head | 0/3 | 0.00 | 108.49 | — |

[Raw shutdown profiles](drain-profile.jsonl)

288 observations from a superseded candidate are available separately. They are excluded from this release table. [Intermediate observations](intermediate-observations.jsonl)

[Statistics](statistics.csv) · [Paired raw observations](observations.jsonl) · [Method](METHOD.md) · [Environment](machine.json)

## Reproduction and downloads

The [complete verification archive](verification.zip) includes two independent reproduction kits. The functional kit includes the two CLI configurations, an isolated Docker Compose environment and a saved panel oracle. The benchmark kit contains the workload sources, asset and build preparation commands and process/HTTP probes. Each includes a runbook. Raw timing observations and confidence intervals accompany the benchmark report.
