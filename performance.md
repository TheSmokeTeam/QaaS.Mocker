# QaaS.Mocker CLI performance follow-up

Mocker now indexes successfully loaded processor types once per assembly within a build. It refreshes the assembly set between stubs and retries partial loads so a hook can load another plugin and change later name resolution. Separate builds create fresh indices. At CLI shutdown the existing native Serilog logger uses asynchronous disposal when supported, with synchronous providers and blocking prefixes contained on a worker. The maximum wait remains 125 ms by default and the previous connected-sink and actual-emission guards still apply. Each server keeps its existing local counter window.

The change removes a dedicated thread held up by a delayed acknowledgement. It does not eliminate the short-process peak working-set increase. In 16 independent, healthy, four-arm process blocks, per-process peak working set was a median 126.02 MiB off and 133.34 MiB on. The paired change was +5.84% (95% interval +4.54% to +7.16%). Comparing the previous PR to this candidate with telemetry on gave −0.55% (−2.35% to +1.29%). In this series Elasticsearch accepted complete lifecycle pairs from 13 of 16 candidate launches and 12 of 16 prior launches. A default bounded wait can expire even against a locally healthy service on a busy desktop.

## Source and environment

| Component | Git SHA |
|---|---|
| Merge base | `363f4e22e209952db336558ca8aa0e31f10733b5` |
| Previously reviewed PR | `707d875037018b080a38074208e4e1c562e7685a` |
| Measured candidate | `265859576ea77a85874533ece393a1386d83df8d` |

Windows 10 Home, Intel Core i5-1135G7, four physical cores, eight logical processors and 7.7 GiB usable memory. .NET SDK 10.0.203. CPython 3.14.5 sampled each Windows process through launch and exit. The Docker Elasticsearch 7.17.29 container had a 640 MiB limit. The existing ChatGPT desktop session remained open. Console output, CPU, memory, sink endpoint, execution inputs and deadlines were matched within a block. The raw receipts include each binary digest and measured outcomes. [Original verification and dashboards](https://github.com/TheSmokeTeam/QaaS.Mocker/blob/4abc87559203d66b54d58adf319af52f2224f43f/verification.md)

## CLI wall time

Each row reports a paired geometric change and two-sided 95% t interval on log ratios. The columns report marginal medians. A positive percentage means more elapsed time for the work. The upper interval must be at most +2% to confirm the requested overhead ceiling. Every launched block is present in the raw evidence. The printers and network receipt checks ran outside timed intervals. This is a process-level study on one physical machine.

| Scenario | Comparison | Pairs | Reference median | Candidate median | Paired change (95% interval) |
|---|---|---:|---:|---:|---:|
| mocker-budget-healthy | head-on → head-ms32 | 6 | 2.352 s | 2.209 s | -7.94% [-17.51%, +2.74%] |
| mocker-budget-healthy | head-on → head-ms75 | 6 | 2.352 s | 2.160 s | -7.30% [-19.35%, +6.54%] |
| mocker-budget-slow | head-on → head-ms32 | 6 | 2.177 s | 2.304 s | +11.28% [-1.89%, +26.21%] |
| mocker-budget-slow | head-on → head-ms75 | 6 | 2.177 s | 2.242 s | +8.17% [-13.85%, +35.81%] |
| mocker-disabled | base-off → head-on | 4 | 1.881 s | 1.730 s | +1.04% [-32.54%, +51.34%] |
| mocker-disabled | prior-on → head-on | 4 | 2.326 s | 1.730 s | -21.49% [-61.44%, +59.88%] |
| mocker-disabled | head-off → head-on | 4 | 1.620 s | 1.730 s | +11.11% [-3.10%, +27.40%] |
| mocker-failure-healthy | prior-on → head-on | 4 | 2.251 s | 3.331 s | +13.67% [-55.84%, +192.57%] |
| mocker-failure-healthy | head-off → head-on | 4 | 2.215 s | 3.331 s | +40.98% [-10.52%, +122.13%] |
| mocker-failure-slow | prior-on → head-on | 4 | 2.344 s | 2.294 s | +8.99% [-33.60%, +78.90%] |
| mocker-failure-slow | head-off → head-on | 4 | 2.064 s | 2.294 s | +22.45% [-29.47%, +112.60%] |
| mocker-failure-unreachable | prior-on → head-on | 4 | 5.682 s | 5.981 s | +5.49% [-4.30%, +16.29%] |
| mocker-failure-unreachable | head-off → head-on | 4 | 5.841 s | 5.981 s | +1.59% [-5.29%, +8.97%] |
| mocker-healthy | base-off → head-on | 16 | 2.625 s | 2.728 s | -10.04% [-32.29%, +19.54%] |
| mocker-healthy | prior-on → head-on | 16 | 2.552 s | 2.728 s | +11.74% [-8.69%, +36.74%] |
| mocker-healthy | head-off → head-on | 16 | 2.249 s | 2.728 s | +19.64% [-2.96%, +47.49%] |
| mocker-slow | base-off → head-on | 8 | 2.377 s | 2.485 s | +3.38% [-29.60%, +51.83%] |
| mocker-slow | prior-on → head-on | 8 | 2.876 s | 2.485 s | -4.27% [-44.59%, +65.40%] |
| mocker-slow | head-off → head-on | 8 | 2.549 s | 2.485 s | +13.85% [-19.81%, +61.64%] |
| mocker-unreachable | base-off → head-on | 6 | 6.424 s | 6.213 s | +5.14% [-15.94%, +31.52%] |
| mocker-unreachable | prior-on → head-on | 6 | 6.409 s | 6.213 s | -2.33% [-23.69%, +25.01%] |
| mocker-unreachable | head-off → head-on | 6 | 6.361 s | 6.213 s | -2.49% [-17.21%, +14.86%] |

## Peak working set

Windows GetProcessMemoryInfo reports the process high-water mark. The harness reads it at intervals of up to 25 ms while the child is alive. Private bytes are also recorded in the CSV. A peak working set is resident memory and includes managed, native, and mapped pages.

| Scenario | Comparison | Pairs | Reference median | Candidate median | Paired change (95% interval) |
|---|---|---:|---:|---:|---:|
| mocker-budget-healthy | head-on → head-ms32 | 6 | 132.43 MiB | 129.49 MiB | -2.36% [-4.25%, -0.43%] |
| mocker-budget-healthy | head-on → head-ms75 | 6 | 132.43 MiB | 132.75 MiB | +0.26% [-1.96%, +2.53%] |
| mocker-budget-slow | head-on → head-ms32 | 6 | 134.58 MiB | 129.13 MiB | -3.56% [-4.85%, -2.26%] |
| mocker-budget-slow | head-on → head-ms75 | 6 | 134.58 MiB | 133.41 MiB | -0.76% [-3.00%, +1.53%] |
| mocker-disabled | base-off → head-on | 4 | 107.40 MiB | 107.25 MiB | +0.08% [-0.84%, +1.01%] |
| mocker-disabled | prior-on → head-on | 4 | 107.40 MiB | 107.25 MiB | -1.00% [-3.96%, +2.05%] |
| mocker-disabled | head-off → head-on | 4 | 107.14 MiB | 107.25 MiB | +0.24% [-0.39%, +0.87%] |
| mocker-failure-healthy | prior-on → head-on | 4 | 139.74 MiB | 139.15 MiB | -0.24% [-1.41%, +0.94%] |
| mocker-failure-healthy | head-off → head-on | 4 | 135.84 MiB | 139.15 MiB | +2.57% [+1.48%, +3.67%] |
| mocker-failure-slow | prior-on → head-on | 4 | 138.75 MiB | 137.42 MiB | -0.94% [-1.38%, -0.50%] |
| mocker-failure-slow | head-off → head-on | 4 | 135.84 MiB | 137.42 MiB | +1.36% [+0.52%, +2.21%] |
| mocker-failure-unreachable | prior-on → head-on | 4 | 125.38 MiB | 125.36 MiB | -0.09% [-0.75%, +0.57%] |
| mocker-failure-unreachable | head-off → head-on | 4 | 124.87 MiB | 125.36 MiB | +0.38% [-0.23%, +1.00%] |
| mocker-healthy | base-off → head-on | 16 | 126.41 MiB | 133.34 MiB | +6.01% [+4.45%, +7.59%] |
| mocker-healthy | prior-on → head-on | 16 | 133.86 MiB | 133.34 MiB | -0.55% [-2.35%, +1.29%] |
| mocker-healthy | head-off → head-on | 16 | 126.02 MiB | 133.34 MiB | +5.84% [+4.54%, +7.16%] |
| mocker-slow | base-off → head-on | 8 | 126.57 MiB | 134.98 MiB | +6.02% [+4.78%, +7.28%] |
| mocker-slow | prior-on → head-on | 8 | 133.25 MiB | 134.98 MiB | +0.71% [-1.31%, +2.78%] |
| mocker-slow | head-off → head-on | 8 | 126.51 MiB | 134.98 MiB | +6.08% [+4.77%, +7.41%] |
| mocker-unreachable | base-off → head-on | 6 | 115.62 MiB | 116.21 MiB | +0.33% [-1.05%, +1.74%] |
| mocker-unreachable | prior-on → head-on | 6 | 115.67 MiB | 116.21 MiB | -0.34% [-1.92%, +1.27%] |
| mocker-unreachable | head-off → head-on | 6 | 116.19 MiB | 116.21 MiB | -0.48% [-1.76%, +0.81%] |

## Native Elasticsearch receipts

The measurement relay forwards each request to the actual local Elasticsearch instance and counts successful per-item bulk results. The slow condition holds a received bulk for one second before forwarding. Server receipts after process exit are recorded separately from wall timing. A refused connection yields no receipt. Each measured execution is expected to produce one start and one terminal when usage is enabled. The denominator includes every expected event. Wire sizes are compressed HTTP body bytes; decoded bulk bytes are recorded separately. Elasticsearch storage bytes depend on mappings and retention. The relay belongs to the experiment. The product uses the configured native logger directly.

| Scenario | Candidate | Starts indexed / expected | Terminals indexed / expected | Median compressed bulk bytes |
|---|---|---:|---:|---:|
| mocker-budget-healthy | head-ms32 | 0 / 6 | 0 / 6 | 0 |
| mocker-budget-healthy | head-ms75 | 5 / 6 | 5 / 6 | 638 |
| mocker-budget-healthy | head-on | 6 / 6 | 6 / 6 | 638 |
| mocker-budget-slow | head-ms32 | 0 / 6 | 0 / 6 | 0 |
| mocker-budget-slow | head-ms75 | 5 / 6 | 5 / 6 | 638 |
| mocker-budget-slow | head-on | 6 / 6 | 6 / 6 | 639 |
| mocker-failure-healthy | head-on | 4 / 4 | 4 / 4 | 647 |
| mocker-failure-healthy | prior-on | 4 / 4 | 3 / 4 | 646 |
| mocker-failure-slow | head-on | 4 / 4 | 4 / 4 | 646 |
| mocker-failure-slow | prior-on | 4 / 4 | 4 / 4 | 646 |
| mocker-framework-healthy | lean-on | 7 / 8 | 7 / 8 | 638 |
| mocker-framework-healthy | stock-on | 7 / 8 | 7 / 8 | 638 |
| mocker-framework-slow | lean-on | 4 / 4 | 4 / 4 | 639 |
| mocker-framework-slow | stock-on | 4 / 4 | 4 / 4 | 639 |
| mocker-healthy | head-on | 13 / 16 | 13 / 16 | 637 |
| mocker-healthy | prior-on | 12 / 16 | 12 / 16 | 638 |
| mocker-slow | head-on | 8 / 8 | 8 / 8 | 638 |
| mocker-slow | prior-on | 7 / 8 | 7 / 8 | 638 |

## Short process delivery and the shutdown budget

The normal redirected-input CLI was timed from process launch through actual exit. Its short execution and startup-error variants also ran with a separate startup-hook observer. The observer measured the disposal callback itself. In the three delayed normal runs the callback took 126.7–143.0 ms. In three delayed failure runs it took 125.7–130.1 ms. OS scheduling can extend the measured callback past the 125 ms wait argument. The callback recorded zero elapsed work on all six refusal probes. A one-second delayed acknowledgement can arrive after process exit if the native client submitted the bulk by the deadline.

The wait-only experiment used the same release binary. Each cell is a matched lifecycle pair accepted by Elasticsearch per six launched processes.

| Endpoint | Configured wait, ms | Matched / expected |
|---|---:|---:|
| healthy | 32 | 0/6 |
| healthy | 75 | 5/6 |
| healthy | 125 | 6/6 |
| slow | 32 | 0/6 |
| slow | 75 | 5/6 |
| slow | 125 | 6/6 |

The separate healthy functional check delivered 6/6 serial and 6/6 overlapping attempts, split between successful and failing processes. The 13/16 result in the main healthy benchmark still counts. A 32 ms default lost all measured lifecycle pairs in both wait experiments. This PR retains 125 ms and the documented override. Refused connections, already-connected slow writes and healthy short processes have different delivery characteristics.

## Working-set investigation

In six separate EventPipe profiles, the enabled process acquired 9.23–12.26 MiB of resident memory during the shutdown callback. Each enabled callback overlapped 13 ASP.NET/Kestrel module loads. Three enabled profiles recorded both network-sink activity and host construction. The normal `--run-locally` path with redirected stdin starts the built-in HTTP task and immediately requests shutdown. Giving the sink time to drain also gives that task time to build the web host. Resident pages, sampled managed allocations and inclusive CPU stacks measure different quantities. The trace does not allocate all extra memory to one subsystem.

A local-only framework transport A/B at Framework 1.7.0 measured omitting the native debug option at +0.25% peak [−2.81%, +3.42%] with telemetry enabled. Both transport builds delivered 7/8 healthy pairs. See `checks/native-profile.md`, the anonymized native profile archive and `checks/local-framework-provenance.md`. No package change was shipped. The standard CLI continues to perform its normal ASP.NET startup and bind-time validation.

## Independent high-RPS host pairs

One independently restarted process pair per block, alternating launch order and ports. Each host warmed up with 5,000 requests, then served a five-second mixed load with 16 concurrent clients. The client measured individual request latency. RPS overhead is reference throughput divided by candidate throughput. Other overhead is candidate divided by reference. Median columns use each metric’s native unit. Healthy and slow delivery counts include the readiness request and warmup. The refusal endpoint has no receipt.

| Comparison | Metric | Independent pairs | Reference median | Candidate median | Overhead, 95% interval |
|---|---|---:|---:|---:|---:|
| base-healthy | rps | 3 | 7134.26 | 7451.36 | +9.83% [-40.90%, +104.13%] |
| base-healthy | p95_ms | 3 | 6.17 | 6.19 | +18.51% [-22.82%, +81.99%] |
| base-healthy | cpu_us_per_req | 3 | 307.02 | 283.66 | +8.03% [-23.29%, +52.13%] |
| base-slow | rps | 3 | 5017.88 | 5348.27 | -14.21% [-55.18%, +64.21%] |
| base-slow | p95_ms | 3 | 9.88 | 9.60 | -13.13% [-46.54%, +41.14%] |
| base-slow | cpu_us_per_req | 3 | 460.53 | 390.16 | -14.47% [-48.73%, +42.69%] |
| base-unreachable | rps | 3 | 7459.76 | 6946.55 | +28.06% [-50.59%, +231.91%] |
| base-unreachable | p95_ms | 3 | 7.57 | 6.50 | +23.06% [-63.84%, +318.88%] |
| base-unreachable | cpu_us_per_req | 3 | 320.87 | 329.28 | +18.78% [-39.75%, +134.19%] |
| prior-healthy | rps | 3 | 6055.57 | 6259.42 | +5.60% [-63.36%, +204.31%] |
| prior-healthy | p95_ms | 3 | 7.87 | 7.05 | +4.18% [-45.79%, +100.18%] |
| prior-healthy | cpu_us_per_req | 3 | 381.32 | 374.82 | +0.46% [-60.72%, +156.91%] |
| switch-healthy | rps | 4 | 5734.73 | 4303.44 | +49.70% [-33.60%, +237.47%] |
| switch-healthy | p95_ms | 4 | 8.02 | 12.01 | +64.43% [-34.84%, +314.98%] |
| switch-healthy | cpu_us_per_req | 4 | 387.06 | 542.01 | +37.06% [-34.66%, +187.50%] |
| switch-slow | rps | 3 | 5017.53 | 5080.90 | -0.60% [-38.88%, +61.67%] |
| switch-slow | p95_ms | 3 | 9.55 | 9.60 | +0.88% [-14.83%, +19.48%] |
| switch-slow | cpu_us_per_req | 3 | 427.00 | 421.57 | -4.61% [-38.46%, +47.87%] |
| switch-unreachable | rps | 3 | 6442.02 | 6098.39 | +19.58% [-77.86%, +545.92%] |
| switch-unreachable | p95_ms | 3 | 7.13 | 8.13 | +17.87% [-75.82%, +474.64%] |
| switch-unreachable | cpu_us_per_req | 3 | 359.92 | 312.56 | +9.80% [-77.72%, +441.01%] |

| Receiving endpoint | Candidate host pairs | Client requests incl. warmup | Requests accounted in summaries |
|---|---:|---:|---:|
| base-healthy | 3 | 116,770 | 116,770 |
| base-slow | 3 | 95,677 | 95,677 |
| prior-healthy | 3 | 107,914 | 107,914 |
| switch-healthy | 4 | 101,685 | 101,685 |
| switch-slow | 3 | 88,991 | 88,991 |

## Commands for the measured Mocker comparisons

The short-process primary uses four arms, with both the prior and selected binaries measured with usage enabled. The failure fixture exercises a real startup exception. Build the observer from the extracted archive with `dotnet build ../revision-bench/DrainHook/DrainHook.csproj -c Release` before running `mocker_phases.py`. The separate high-RPS hosting experiment restarts each Kestrel pair and checks actual responses.

```powershell
python suite.py Mocker primary
python suite.py Mocker failure
python mocker_phases.py
python short_receipts.py
foreach ($mode in 'healthy','slow') { python paired.py Mocker $mode 6 "replication-budget-$mode.jsonl" head-on head-ms32 head-ms75 }
python http_pairs.py switch 4
python http_pairs.py prior 3
foreach ($mode in 'healthy','slow','unreachable') { python http_pairs.py base 3 --mode $mode }
foreach ($mode in 'slow','unreachable') { python http_pairs.py switch 3 --mode $mode }
python http_stats.py
```

## Cross-product high-assertion workload

Each Runner launch made a witnessed successful HTTP call to Mocker, executed all 128 named assertions and returned the expected failure exit code. Each mode had three alternating pairs on the selected binaries with usage off/on. Values below are marginal medians. The paired wall column gives estimate / lower / upper 95% limit.

| Endpoint | Median wall off / on (s) | Assertions/s off / on | Median child CPU off / on (s) | Paired wall change |
|---|---:|---:|---:|---:|
| healthy | 9.41 / 5.14 | 13.60 / 24.91 | 9.00 / 6.38 | -28.74% / -60.16% / +27.46% |
| slow | 10.95 / 11.11 | 11.69 / 11.52 | 7.33 / 7.55 | -2.31% / -16.01% / +13.62% |

## Functional verification at the measured commit

The live fixture produced three starts and two observed finishes (one success and one Mocker error). A third Mocker was deliberately killed after its start was indexed. The ten witnessed HTTP requests produced two misses, one handler error and two HTTP 5xx responses. The deliberately mocked 503 contributed only to HTTP 5xx. The idle window emitted zero summaries. All 348 dashboard comparisons across 12 filter sets agreed with the local oracle. The original miss, fallback, handler failure, startup error and shutdown diagnostics were observed.

All 488 Release tests passed. Push and pull-request CI passed at this SHA and the review-thread audit found zero unresolved threads. Human review is pending. [Pull-request CI](https://github.com/TheSmokeTeam/QaaS.Mocker/actions/runs/45574930326)

## Reproduce

On Windows, extract measurements.zip into an empty directory. Create repository checkouts named `Runner`, `RunnerBase`, `Mocker` and `MockerBase` beside `perf-followup`. Check out the candidate and merge-base SHAs and build each Release solution with a single MSBuild node. Restore the same packages recorded in the original report. Build the prior PR in a separate checkout and copy its entire `net10.0` Release host output into `perf-followup/before-Mocker`. The Runner host project is `QaaS.Runner.E2ETests` and the Mocker host project is `QaaS.Mocker.Example`.

Start the isolated ES7 container mapped to 127.0.0.1:19278 as described in the [original verification](https://github.com/TheSmokeTeam/QaaS.Mocker/blob/4abc87559203d66b54d58adf319af52f2224f43f/verification.md). The refusal case expects port 19996 to be unused. From the extracted archive root, use `python reproduce.py` to generate the fixed batch fixture and rewrite its absolute paths for your checkout. Execute the benchmark scripts from `perf-followup`. The original observations are kept under `measurements` and `checks`, separate from new output. Builds and downloads occur outside timed intervals. The labelled unprimed calibration includes its cold launch.

```powershell
dotnet build QaaS.Mocker.sln -c Release --no-restore -m:1 -p:UseSharedCompilation=false
dotnet test QaaS.Mocker.sln -c Release --no-build --no-restore -m:1
python suite.py Mocker primary
```

The full paired statistics, CLI command construction, fixed fixture, seed and individual measurements are in the downloadable archive. The source refs above identify the product code.

For the live comparison, use the isolated Docker network, Elasticsearch transforms, Grafana data sources and dashboards described in the original verification. The test harness expects the local `mamtzetza-mocker` runtime image, an attached `qaas-pr45-local` network, the ES container `qaas-pr78-es`, Grafana on port 13045 and Elasticsearch on port 19278. From `revision-verification`, run `python live.py`, `python check_final_live.py` and `python debug_check.py` in order. The window that extends 20 minutes past the last event tests missing finishes after the dashboard’s 15-minute grace period. The archive includes the expected event counts, filter comparison results and local workload drivers.
