# AGENTS.md — QaaS.Mocker
Guidance for AI agents working in this repository.

## What this repo is
QaaS.Mocker is the configurable mock-server engine (Tier 2). It hosts protocol servers
(HTTP/HTTPS, gRPC/gRPCs, TCP socket), routes traffic to stubs, and processes requests via
`ITransactionProcessor` hooks. A Redis-backed control plane (`QaaS.Mocker.Controller`) lets a
running QaaS.Runner mutate mocks mid-test via Redis command channels. Target: net10.0.

## Projects / Layout
| Project | Purpose |
|---|---|
| QaaS.Mocker | CLI parser, Autofac root, config loader, `MockerRunner.cs` multi-mocker concurrent orchestrator |
| QaaS.Mocker.Stubs | Four-stage pipeline: Deserialize Request → Process Stub → Serialize Response → Validate Shape |
| QaaS.Mocker.Servers | Kestrel HTTP, ASP.NET Core gRPC, native TCP socket bindings |
| QaaS.Mocker.Controller | Redis command plane → routes runtime mutations to stubs |
| QaaS.Mocker.Example | Runnable example: mock endpoints, DevCert generators, TCP broadcasters |
| QaaS.Mocker.CliExport | Exports mock schemas for the docs generator |

## Build & test
```shell
dotnet build -m QaaS.Mocker.sln
dotnet test QaaS.Mocker.sln
# Canonical run:
dotnet run --project QaaS.Mocker -- run mocker.qaas.yaml
# Lint/template check:
dotnet run --project QaaS.Mocker -- -m Lint mocker.qaas.yaml
```
Config overlays: `-w` ordered files, `-f` alphabetical folders, `-r` inline path overrides
(e.g. `Servers:0:Http:Port=9000`), `--no-env` blocks env-var overrides.

## YAML config sections
```yaml
Controller:            # optional Redis command channel
  ServerName: X
  Redis: { Host: localhost, RedisDataBase: 0 }
Stubs:
  - { Name: ExampleStub, Processor: ExampleProcessor }
Servers:
  - Http:  { Port, IsSecuredSchema, CertificatePath, CertificatePassword,
             Endpoints: [ { Path, Actions: [ { Name, Method, TransactionStubName } ] } ] }
  - Grpc:  { Port, Services: [ { ServiceName, ProtoNamespace, AssemblyName,
             Actions: [ { Name, RpcName, TransactionStubName } ] } ] }
  - Socket:{ BindingIpAddress, Endpoints: [ { Port, ProtocolType: Tcp, SocketType: Stream,
             Action: { Name, Method: Collect|Broadcast, TransactionStubName } } ] }
```

## Redis control plane
- **Request channel**: `runner-to-mocker:{contentType}:{serverName}` (lowercase)
- **Response channel**: `mocker-to-runner:{contentType}:{serverName}` (lowercase)
- **Commands**: `ChangeActionStub` (swap active stub), `TriggerAction` (force-fire, e.g. socket
  broadcast), `Consume` (fetch buffered endpoint logs), `Ping`, `Status`
- DTOs live in `Qaas.Mocker.CommunicationObjects` (separate Tier-1 repo).

## Critical gotchas
- **Tier-2, depends on QaaS.Framework.Executions 1.5.1** — pin this version; a Framework
  breaking change requires coordinating updates here.
- **`ITransactionProcessor`** is the sole hook type used — implement it in an assembly
  discoverable by Framework.Providers scan order (`QaaS.*` → `Common.*` → user libs).
- **Concurrent MockerExecutionBuilders run in separate Autofac scopes** (PR #27) — do NOT
  share mutable state across mocker instances.
- **Redis channel names are lowercase** (despite PascalCase YAML keys) — case mismatch causes
  silent channel misses; Runner and Mocker must use identical channel strings.
- **`Qaas.Mocker.CommunicationObjects`** (note lowercase 'aas', separate Tier-1 repo) holds
  DTOs shared with Runner — always keep both repos in sync when changing commands.
- **`--run-locally` flag** attaches the process to the console with keypress stop — never use
  in unattended CI scripts.
- **DevCerts in QaaS.Mocker.Example** are for local development only; never commit real certs.

## Process
Follow the QaaS harness pipeline: plan → contract → implement → adversarial evaluation
(rubric: Correctness/Completeness/Craft/Robustness, each ≥7/10). Write tests first (TDD).
Conventional commits: `feat:`, `fix:`, `chore(release):`.
Run `dotnet format` before committing.
