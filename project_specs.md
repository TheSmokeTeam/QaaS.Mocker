# project_specs.md — QaaS.Mocker Solution

> Architectural specification for the **QaaS.Mocker** repository.
> See `CLAUDE.md` for the AI operating manual, and per-project
> `project_specs.md` files inside each `QaaS.Mocker.*` directory.
> Live docs: <https://docs.qaas.online/>.

## 1. Purpose

`QaaS.Mocker` is the configurable mock-server runtime of the QaaS platform.
It loads a YAML mocker definition, hosts protocol-bound mock servers,
matches incoming traffic to **Stubs**, runs **Processors** to derive
responses, and optionally exposes a Redis-backed control plane so a paired
`QaaS.Runner` can mutate stub behaviour mid-test.

It is hook-agnostic: every processor is an `ITransactionProcessor`
implementation discovered via `QaaS.Framework.Providers`. The shipped
Common processors live in `QaaS.Common.Processors`.

## 2. Scope and non-goals

In scope:

- CLI parsing (`run`, `template`).
- YAML loading + validation + reference/placeholder resolution.
- Composition of Servers, Stubs, and Processors via the fluent builder.
- Hosting protocol servers (HTTP/HTTPS, gRPC/gRPCs, Socket broadcast +
  collect, Kafka/RabbitMQ stubs).
- Redis control plane for runtime stub manipulation.
- Multi-execution concurrency.

Out of scope:

- Concrete processor implementations — `QaaS.Common.Processors` and user
  assemblies.
- Test orchestration — `QaaS.Runner` owns that.
- Wire-format details for the Runner ↔ Mocker control plane —
  `Qaas.Mocker.CommunicationObjects` owns those contracts.

## 3. Solution structure

| Project | Role | Depends on |
|---|---|---|
| `QaaS.Mocker` | CLI, builder, runner, validation. | All siblings, `Framework.{Executions,SDK,Providers,Configurations,Serialization,Protocols}`, `Autofac`. |
| `QaaS.Mocker.Stubs` | Stub runtime + processor pipeline. | `Framework.SDK`, `Framework.Serialization`. |
| `QaaS.Mocker.Servers` | Per-protocol server hosts. | `QaaS.Mocker.Stubs`, `Framework.Protocols`, `Microsoft.AspNetCore.*` (Kestrel), `Grpc.AspNetCore`. |
| `QaaS.Mocker.Controller` | Redis control plane. | `Qaas.Mocker.CommunicationObjects`, `StackExchange.Redis`. |
| `QaaS.Mocker.Example` | Runnable example. | All siblings + protobuf-net. |
| `QaaS.Mocker.CliExport` | CLI/schema export tool used by docs gen. | `QaaS.Mocker`. |
| `QaaS.Mocker.{*}.Tests` | NUnit + Moq. | Their target. |

## 4. Public surface

### 4.1 CLI

| Command | Purpose |
|---|---|
| `run` | Load YAML, build, start servers, optionally connect controller, block. |
| `template` | Scaffold a new mocker project from `QaaS.Mocker.Template`. |

### 4.2 Code-first

`MockerExecutionBuilder` is the fluent root. The pattern mirrors
`QaaS.Runner`: builders are split into `*Properties` / `*Logic` /
`*Validation` partials. `Server` / `Servers` are mutually exclusive.

### 4.3 YAML

Top-level: `MetaData`, `Variables`, `DataSources`, `Stubs`, `Server` *or*
`Servers`, `Controller` (optional). Sub-shapes documented at
<https://docs.qaas.online/mocker/configurationSections/>.

## 5. Stub execution model

Per stub:

1. Match — request matches stub criteria (path, method, headers, body
   pattern, etc.) selected by the server's routing layer.
2. Deserialize — bytes → `Data<object>` using the configured deserializer.
3. Process — call `ITransactionProcessor.Process(dataSources, request)`.
4. Serialize — response → bytes via the configured serializer.
5. Validate — DataAnnotations on the response object (status code range,
   header presence, etc.).

Errors return a typed protocol-appropriate error response and log the
original failure with full request context.

## 6. Multi-execution model

`MockerRunner.Run()` schedules each `Execution.Start()` via `Task.Run`,
awaits all, returns the worst exit code. Each Execution owns its own
Autofac scope and server lifecycle. PR #27 (commit `3469179`) introduced
this behaviour.

## 7. Control plane (Redis)

Implementation: `QaaS.Mocker.Controller`. Wire format:
`Qaas.Mocker.CommunicationObjects` (separate repo, separate NuGet).

Channels (lowercased, deterministic):

- `qaas:mocker:{name}:ping`
- `qaas:mocker:{name}:command`
- `qaas:mocker:{name}:command-response`

Commands: `ChangeActionStub`, `TriggerAction`, `Consume`, `Ping`, `Status`.

The Runner sends these via its `MockerCommands` action type.

## 8. Build, packaging, CI

- Target: `.NET 10.0`, nullable enabled, `TreatWarningsAsErrors=true`.
- NuGet identity: `QaaS.Mocker` (CLI/library), `QaaS.Mocker.Stubs`,
  `QaaS.Mocker.Servers`, `QaaS.Mocker.Controller` — versioned in lockstep.
- Docker: `Dockerfile` builds a self-contained image.
- CI: `.github/workflows/ci.yml` — restore → build → test → coverage →
  pack-and-push on stable tags.
- Coverage badges: linked from README.

## 9. Quality requirements

- Same partial-class convention as Runner.
- Validation rules expressed as bespoke `ValidationAttribute`s, not runtime
  `if/throw`.
- All public processors must inherit `BaseTransactionProcessor<TConfig>`.

## 10. Compatibility & versioning

Public surface = `MockerExecutionBuilder`, CLI verbs, YAML schema, control
plane wire types (re-exported from `Qaas.Mocker.CommunicationObjects`).
SemVer per stable git tag.

## 11. Roadmap signals

- PR #29 — CLAUDE.md / project_specs.md.
- PR #28 — builder cloning.
- PR #27 — concurrent execution (merged April 2026).

## 12. References

- Live docs: <https://docs.qaas.online/mocker/>
- Comm objects: `Qaas.Mocker.CommunicationObjects` repo.
- Smoke harness: `MockerAlphaSmokeApp`.
