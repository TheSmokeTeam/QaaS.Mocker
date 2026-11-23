# QaaS.Mocker

Configurable mock runtime for QaaS protocol workloads.

[![CI](https://github.com/TheSmokeTeam/QaaS.Mocker/actions/workflows/ci.yml/badge.svg)](https://github.com/TheSmokeTeam/QaaS.Mocker/actions/workflows/ci.yml)
[![Line Coverage](https://img.shields.io/endpoint?url=https://gist.githubusercontent.com/eldarush/21e8a633b4f621063f66a2fb3d8839b5/raw/line-coverage-badge.json)](https://github.com/TheSmokeTeam/QaaS.Mocker/actions/workflows/ci.yml)
[![Branch Coverage](https://img.shields.io/endpoint?url=https://gist.githubusercontent.com/eldarush/21e8a633b4f621063f66a2fb3d8839b5/raw/branch-coverage-badge.json)](https://github.com/TheSmokeTeam/QaaS.Mocker/actions/workflows/ci.yml)
[![Docs](https://img.shields.io/badge/docs-qaas--docs-blue)](https://thesmoketeam.github.io/qaas-docs/)
[![.NET](https://img.shields.io/badge/.NET-10.0-512BD4)](https://dotnet.microsoft.com/)

## Contents
- [Overview](#overview)
- [Packages](#packages)
- [Functionalities](#functionalities)
- [Protocol Support](#protocol-support)
- [Quick Start](#quick-start)
- [Run the Example](#run-the-example)
- [Runner Integration Overlay](#runner-integration-overlay)
- [Build and Test](#build-and-test)
- [Documentation](#documentation)

## Overview
This repository contains one solution: [`QaaS.Mocker.sln`](./QaaS.Mocker.sln).

`QaaS.Mocker` is published as a single NuGet package and composes internal runtime modules for stubs, protocol servers, and optional Redis controller operations.

## Packages
| Package | Latest Version | Total Downloads |
|---|---|---|
| [QaaS.Mocker](https://www.nuget.org/packages/QaaS.Mocker/) | [![NuGet](https://img.shields.io/nuget/v/QaaS.Mocker?logo=nuget)](https://www.nuget.org/packages/QaaS.Mocker/) | [![Downloads](https://img.shields.io/nuget/dt/QaaS.Mocker?logo=nuget)](https://www.nuget.org/packages/QaaS.Mocker/) |

## Functionalities
### [QaaS.Mocker](./QaaS.Mocker/)
- Parses CLI options and loads YAML configuration with overwrite files/arguments and optional environment resolution.
- Builds and validates execution context, data sources, stubs, one or more server runtimes, and optional controller runtime.
- Supports execution modes: `Run` and `Template`.

### [QaaS.Mocker.Stubs](./QaaS.Mocker.Stubs/)
- Builds transaction stubs from configured processors and data source bindings.
- Executes request/response transformation through configurable serializer/deserializer pairs.
- Includes default status-code stubs for not-found and internal-error flows.

### [QaaS.Mocker.Servers](./QaaS.Mocker.Servers/)
- Hosts protocol runtimes for `HTTP/HTTPS`, `gRPC/gRPCs`, and `Socket`.
- Routes actions/RPC methods to transaction stubs through state objects.
- Caches input/output payloads for controller consume operations.

### [QaaS.Mocker.Controller](./QaaS.Mocker.Controller/)
- Initializes optional Redis-backed runtime controller when configured.
- Handles `Ping` and `Command` channels.
- Supports runtime commands such as action-to-stub switch, trigger, and consume.

### [QaaS.Mocker.Example](./QaaS.Mocker.Example/)
- Provides runnable multi-server and transport-specific mocker configurations, sample processors, protobuf schema, and sample data assets.

## Protocol Support
Supported protocol/runtime families in `QaaS.Mocker`:

| Family | Implementations |
|---|---|
| HTTP / RPC | HTTP, HTTPS, gRPC, gRPCs |
| Streaming / Socket | Socket (broadcast and collect modes) |
| Runtime Control | Redis-backed controller channels (ping and command) |

## Quick Start
Install package:

```bash
dotnet add package QaaS.Mocker
```

Upgrade package:

```bash
dotnet add package QaaS.Mocker --version <target-version>
dotnet restore
```

## Run the Example
The example uses relative paths for certificates, sample data, and socket payloads, so run it from [`QaaS.Mocker.Example`](./QaaS.Mocker.Example/). The example entry point also disables environment-variable overrides by default so IDE terminal variables do not rewrite the sample configuration.

1. Open a terminal in the example directory.

```powershell
Set-Location .\QaaS.Mocker.Example
```

2. Create the development certificate expected by the sample HTTPS and gRPC configs.

```powershell
dotnet dev-certs https -ep .\Certificates\devcert.pfx -p qaas-dev-cert
```

3. Start the combined example. It boots HTTPS, gRPC with TLS, and a TCP socket collect endpoint from the same process.

```powershell
dotnet run -- run mocker.qaas.yaml
```

4. In a second terminal, call the HTTPS health endpoint.

```powershell
curl.exe -k https://127.0.0.1:8443/health
```

5. Call the gRPC endpoint from Git Bash, WSL, or another POSIX-style shell if you have [`grpcurl`](https://github.com/fullstorydev/grpcurl) installed.

```bash
grpcurl -insecure -import-path Protos -proto echo.proto -d '{"message":"hello"}' 127.0.0.1:50051 qaas.mocker.example.EchoService/Echo
```

6. Send a payload to the socket collect endpoint. The socket endpoint does not reply; success is a clean connect and write plus a collect log entry in the server terminal.

```powershell
$client = [System.Net.Sockets.TcpClient]::new()
$client.Connect('127.0.0.1', 7001)
$stream = $client.GetStream()
$payload = [System.Text.Encoding]::UTF8.GetBytes('socket-check')
$stream.Write($payload, 0, $payload.Length)
$stream.Flush()
$stream.Dispose()
$client.Dispose()
```

7. Generate the effective combined configuration with the `template` command.

```powershell
dotnet run -- template mocker.qaas.yaml
```

8. If you still want the dedicated gRPC-only sample, it remains available.

```powershell
dotnet run -- run mocker.grpc.qaas.yaml
```

## Runner Integration Overlay
The example now includes [`mocker.runner.qaas.yaml`](./QaaS.Mocker.Example/mocker.runner.qaas.yaml), an overwrite file that appends only the runner-integration pieces missing from [`mocker.qaas.yaml`](./QaaS.Mocker.Example/mocker.qaas.yaml):

- Redis controller configuration for runner mocker commands
- Alternate stubs for `ChangeActionStub` checks on HTTP, gRPC, and socket
- A socket broadcast endpoint on port `6000` for `TriggerAction`

The overlay uses explicit numeric indexes for list sections so it can be appended with `--overwrite-files` without editing the base sample.

Use it together with the base file instead of editing the base sample:

1. Start local Redis:

```powershell
docker run -d --name qaas-redis -p 6379:6379 redis:7-alpine
```

2. Ensure the sample certificate exists:

```powershell
Set-Location .\QaaS.Mocker.Example
dotnet dev-certs https -ep .\Certificates\devcert.pfx -p qaas-dev-cert
```

3. Run the sample plus the overlay:

```powershell
dotnet run -- run mocker.qaas.yaml --overwrite-files mocker.runner.qaas.yaml
```

The combined runtime exposes:

- HTTPS health endpoint on `https://127.0.0.1:8443/health`
- TLS gRPC endpoint on `127.0.0.1:50051`
- TCP socket collect endpoint on `127.0.0.1:7001`
- TCP socket broadcast endpoint on `127.0.0.1:6000`
- Redis controller under server name `RunnerMockerExample`

The broadcast endpoint is disabled by default and becomes active only when the runner sends `TriggerAction` for `SocketBroadcastAction`.

## Build and Test
```bash
dotnet restore QaaS.Mocker.sln
dotnet build QaaS.Mocker.sln -c Release --no-restore
dotnet test QaaS.Mocker.sln -c Release --no-build
```

## Execution and HTTP dashboards

Pass `--send-logs --elastic-uri <url>` to use the Framework’s Elasticsearch sink, or configure the existing Elastic logging defaults. Set `MetaData.Team` and `MetaData.System` in the mocker configuration. Both labels are required for the summaries. The Framework’s built-in shipping path enables Debug events for Elasticsearch independently of the console level. A custom logger must allow Debug events. Framework sets `Environment` to `CI` when `KUBERNETES_SERVICE_HOST` is present and to `Local` otherwise.

Import the [Mocker usage dashboard](docs/grafana/mocker-usage.json) into Grafana and select an Elasticsearch data source with index pattern `qaas-*` and time field `@timestamp`. The dashboard detects `.raw` or `.keyword` exact-match fields, so choose an index pattern with a consistent mapping across the time range. A start counts one built execution entering `run`. Failure rate is the fraction of finished executions with `Failed = 1`. Template commands are excluded. Configuration failures before execution, processes still running and abruptly stopped processes do not contribute terminal results. [Dashboard preview](docs/grafana/mocker-usage.png)

Set `Server.Http.EnableHttpMetrics` (or `Servers[].Http.EnableHttpMetrics`) to `true` to log HTTP totals per server instance. Every ten seconds, an active server emits its completed requests since the previous summary. Graceful HTTP host shutdown flushes the final interval. Immediate process exit (including redirected `--run-locally`) can skip that flush. Each event includes `ExecutionId`, `ServerId`, and configured `Port`. Handler error rate uses `sum(HandlerErrors) / sum(Requests)`. It includes exceptions while reading, dispatching or writing the response, including processing failures handled by a fallback stub. `Http5xx` counts responses with a 500–599 status, including intentional stub responses. `Misses` counts unmatched routes or methods that the not-found stub handled successfully. Grafana calculates both error rates across the selected interval using request totals.

The request handler updates one in-memory counter per completion. The summary contains counts and labels only. Hard termination can lose the current interval. If a run emitted usage events and the existing Elasticsearch sink connected during startup, the standard CLI waits for the final batch to finish, for up to two seconds. A short-lived process can exceed 2% wall-time overhead when a previously connected bulk write stalls. When the deadline expires, the last batch can be lost. A sink that never received a successful response during startup keeps its existing shutdown behavior, as does a console-only or file-only logger. Custom embedded runners retain control over their own lifetime. Existing request diagnostics keep their original messages, exceptions and severity levels.

## Documentation

- Official docs: [thesmoketeam.github.io/qaas-docs](https://thesmoketeam.github.io/qaas-docs/)
- CI workflow: [`.github/workflows/ci.yml`](./.github/workflows/ci.yml)
- NuGet package: [QaaS.Mocker on NuGet](https://www.nuget.org/packages/QaaS.Mocker/)
