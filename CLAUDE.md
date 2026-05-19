# CLAUDE.md — QaaS.Mocker Solution

> Operating manual for AI assistants working in the **QaaS.Mocker** repository.
> See `project_specs.md` for the architectural spec, and per-project
> `project_specs.md` files inside each `QaaS.Mocker.*` folder.
> Live docs: <https://docs.qaas.online/>.

## Mission
**QaaS.Mocker** is the QaaS platform's configurable mock runtime. It hosts
protocol-bound mock servers (HTTP/HTTPS, gRPC/gRPCs, Socket
broadcast/collect, Kafka/RabbitMQ stubs), maps incoming requests to
configured **Stubs**, runs **Processors** (`ITransactionProcessor`) to
materialise responses, and optionally exposes a Redis-backed control plane
that the Runner can drive at runtime (`MockerCommands` actions).

It is the lifecycle counterpart to `QaaS.Runner`: the Runner *acts*, the
Mocker *receives and replies*.

## Build / Test / Run

```bash
dotnet build QaaS.Mocker.sln --nologo -clp:ErrorsOnly
dotnet test  QaaS.Mocker.sln --nologo --no-build

# Run the example
dotnet run --project QaaS.Mocker.Example -- run --configuration-path mocker.qaas.yaml

# Format
csharpier format <changed-files>
```

Docker image: `Dockerfile` builds a self-contained mocker image used in
deployments.

## Solution layout

| Project | Purpose |
|---|---|
| `QaaS.Mocker` | CLI entrypoint, YAML loading, `MockerExecutionBuilder` (~900 LoC), `Bootstrap`, `MockerRunner`, validation framework. |
| `QaaS.Mocker.Stubs` | `TransactionStub` (4-stage: Deserialize → Process → Serialize → Validate), processor wiring, stub configuration. |
| `QaaS.Mocker.Servers` | Protocol server runtimes (HTTP/HTTPS, gRPC/gRPCs, Socket, plus message-broker stubs). Each server hosts one set of stubs and reconciles its lifecycle to its Execution. |
| `QaaS.Mocker.Controller` | Optional Redis-backed runtime control plane: `Ping` channel, command channel for `ChangeActionStub`, `TriggerAction`, `Consume`, `Status`. Channel keys built deterministically (lowercased). |
| `QaaS.Mocker.Example` | End-to-end runnable example with a real proto schema and demo processors. |
| `QaaS.Mocker.{,Servers,Stubs,Controller}.Tests` | NUnit + Moq tests. |

## Public API

The fluent builder root is `MockerExecutionBuilder` (in `QaaS.Mocker`):

```csharp
var builder = new MockerExecutionBuilder()
    .WithMetaData(...)
    .WithDataSources(...)
    .WithServer(s => s
        .Named("http-mock")
        .ListenOn("http://+:5000")
        .WithStubs(stub => stub
            .Named("get-user")
            .Match(m => m.WithPath("/users/{id}").WithMethod("GET"))
            .Process(new StaticResponseProcessor { ... })));

int exitCode = builder.Build().Start();
```

`Server` and `Servers` are mutually exclusive at the builder level — pick
one shape, validation enforces it (`ExecutionBuilder.cs:793-798`).

## Concurrent execution model

PR #27 (commit `3469179`, April 2026) added support for **multiple
ExecutionBuilders** running in parallel:

- `MockerRunner` schedules each Execution via `Task.Run` and aggregates
  exit codes.
- Each Execution gets its own Autofac scope (no shared mutable state).
- Long-running scheduling (server lifetime) lives in `Execution.cs`, not
  in `MockerRunner`.

Stubs themselves are stateless — concurrency safety is the responsibility
of the underlying server runtime (HttpListener / Kestrel / gRPC pipeline).

## Stub execution chain (4 stages)

For each incoming request to a server-bound stub:

1. **Deserialize** — bytes → `Data<object>` using the configured
   `DeserializerFactory` (Json/Yaml/Protobuf/Binary/…).
2. **Process** — `ITransactionProcessor.Process(dataSources, request)`
   produces the response `Data<object>`. Hook discovered via
   `QaaS.Framework.Providers`; configured via DataAnnotations.
3. **Serialize** — response `Data<object>` → bytes via the configured
   `SerializerFactory`.
4. **Validate** — DataAnnotations on the response shape (e.g. status code
   in `[Range(100, 599)]`).

If any stage throws, the server returns a typed error and the original
exception is logged via Serilog with the request context attached.

## Control plane (Redis)

`QaaS.Mocker.Controller` listens on Redis channels built by
`Qaas.Mocker.CommunicationObjects.CommunicationMethods`. Channel pattern
(everything lowercased):

```
runner-to-mocker:{contentType}[:{serverName}[:{serverInstanceName}]]
mocker-to-runner:{contentType}[:{serverName}[:{serverInstanceName}]]
```

`contentType` distinguishes payload kind (e.g. `ping`, `command`).
Request channels use the `runner-to-mocker:` prefix; response channels use
`mocker-to-runner:`. Server / instance segments are appended only when the
caller targets a specific server. **Never hard-code these patterns** — go
through `CommunicationMethods.CreateChannel{RunnerToMocker,MockerToRunner}`
and `BaseHandler.RequestChannel` / `ResponseChannel`.

Wire DTOs (`Qaas.Mocker.CommunicationObjects.ConfigurationObjects.Command`):

- `CommandRequest { string Id; CommandType Command; ChangeActionStub?; Consume?; TriggerAction? }` — exactly one of the optional payload properties is populated, matched to `Command`.
- `CommandResponse { string Id; string ServerInstanceId; CommandType Command; Status Status; string? ExceptionMessage }`.
- `CommandType` enum: `ChangeActionStub | TriggerAction | Consume`.
- `Status` enum: `Succeeded | Failed`.
- `Ping`: `PingRequest` / `PingResponse` records.

## Forbidden patterns (NEVER do)

1. Mutating `Server` or `Servers` after `Build()` returns.
2. Configuring **both** `Server` and `Servers` on the same builder
   (mutually exclusive).
3. Calling `Build()` without first cloning the configuration context if
   you intend to reuse the builder.
4. Passing `null` to fluent setters (every `.With…(...)` must validate).
5. Doing long-running scheduling outside `Execution` (the runner aggregates
   exit codes from there).
6. Directly instantiating `TransactionStub` — go through the builder.
7. Mutating stubs after `Build()`; runtime mutation must go through the
   `ChangeActionStub` command.
8. Returning anything other than `byte[]` from a serialiser; the server
   pipeline expects bytes.
9. Duplicate action names across servers in a multi-server execution
   (validation rejects it; do not `[ValidationException]`-suppress).
10. Non-deterministic data-source names (must round-trip through YAML
    cleanly).
11. `[Test(Ignore=…)]` to make a red test green.
12. Hard-coding Redis channel names anywhere outside
    `Qaas.Mocker.CommunicationObjects.CommunicationMethods` (and the
    `BaseHandler` consumers that call into it).

## Must-verify before declaring done

1. `dotnet build QaaS.Mocker.sln` → exit 0.
2. `dotnet test  QaaS.Mocker.sln` → all green (incl. controller tests).
3. Validation results surfaced for every cross-property rule (no silent
   swallows).
4. `Server` / `Servers` mutual-exclusivity holds.
5. Configured processors actually exist (provider resolution succeeds).
6. Configured data sources are defined and reachable.
7. Serialization types match between server and stubs.
8. Redis controller test passes (or is skipped only when a Redis broker is
   genuinely unavailable; never with `[Test(Ignore=…)]`).
9. Long-running flag set correctly on the Execution.
10. CI workflow `.github/workflows/ci.yml` is green.

## Key files for orientation

- `QaaS.Mocker/Bootstrap.cs` — CLI bootstrap.
- `QaaS.Mocker/MockerRunner.cs` — multi-execution orchestrator.
- `QaaS.Mocker/Execution.cs` — single-execution lifecycle / long-running
  schedule.
- `QaaS.Mocker/ExecutionBuilder.cs` (~900 LoC) — root builder + validation.
- `QaaS.Mocker.Stubs/TransactionStub.cs` — 4-stage execution.
- `QaaS.Mocker.Servers/*Server.cs` — per-protocol runtimes.
- `QaaS.Mocker.Controller/*` — Redis control plane.
- `Dockerfile` — production image.

## Recent / in-flight work

- PR #29 (`feature/docs-claude`) — CLAUDE.md drop (this commit).
- PR #28 (`Skidskad:feat/clone-builders`) — `ICloneable<T>` deep-clone for
  mocker builders, paired with Framework PR #31 and Runner PR #28.
- PR #27 — concurrent execution.

## When the docs disagree with the code

Update both. The XML doc comments on builder properties feed
`QaaS.Docs.Generator`; the rendered pages live in `qaas-docs/docs/mocker/`.
