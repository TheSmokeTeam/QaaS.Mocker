# CLAUDE.md — QaaS.Mocker.Controller

See `../CLAUDE.md` for solution-wide rules and `project_specs.md` for the
spec. This file is the working manual for edits inside this project.

## Purpose

Optional Redis-backed control plane that lets a paired `QaaS.Runner`
mutate mocker behaviour at runtime: liveness pings, status inspection,
hot-swapping processors (`ChangeActionStub`), firing `TriggerAction`, and
synchronous `Consume`. Channel construction is delegated to
`Qaas.Mocker.CommunicationObjects.CommunicationMethods` (always
lowercased for deterministic routing).

## Key files

- `ControllerFactory.cs` — builds a `Controller` from configuration.
- `Controllers/Controller.cs` — Redis pub/sub host; owns
  `IConnectionMultiplexer` lifetime, drives `Start`/`Dispose`.
- `Handlers/BaseHandler.cs` — shared subscribe/respond plumbing; computes
  request/response channels via `CommunicationMethods`.
- `Handlers/CommandHandler.cs` — dispatches `CommandRequest` to the
  appropriate per-command implementation.
- `Handlers/PingHandler.cs` — heartbeat / liveness.
- `Extensions/`, `ConfigurationObjects/` — DI helpers and YAML-bound
  controller config.

## Conventions

- Use `StackExchange.Redis` `IConnectionMultiplexer` as a singleton.
- Channel names come from
  `CommunicationMethods.CreateChannel{RunnerToMocker,MockerToRunner}` and
  `BaseHandler.RequestChannel`/`ResponseChannel`. Never construct the
  string by hand.
- Every command handler must be idempotent — Redis pub/sub does not
  guarantee exactly-once delivery.
- Authoritative state lives in the live mocker config / server state, not
  in the controller process.
- Start failures must propagate before any `Thread.Sleep` / retry loop
  (the controller test asserts this explicitly).

## Forbidden in this project

- Hard-coding Redis channel strings anywhere.
- Holding command-derived state in process memory longer than necessary.
- Catching subscription exceptions silently during `Start`.
- Mutating builder/server state from a handler without going through the
  documented command surface (`ChangeActionStub`, `TriggerAction`,
  `Consume`).

## Tests

Project: `QaaS.Mocker.Controller.Tests` (NUnit + Moq).

```bash
dotnet test QaaS.Mocker.Controller.Tests/QaaS.Mocker.Controller.Tests.csproj --nologo
```

Layout:
- `ControllerTests.cs` — lifecycle (Dispose disposes connection,
  subscription failure propagates).
- `ControllerFactoryTests.cs` — wiring.
- `HandlersTests/` — per-command-handler dispatch and round-trips.
- `ConfigurationTests/`, `ExtensionsTests/` — config and DI surface.
- `Globals.cs` — shared Serilog logger fixture.

Mocking convention: stub `IConnectionMultiplexer`, `ISubscriber`,
`IDatabase` with Moq. Live Redis tests use `Testcontainers.Redis` (or
skip based on environment availability) — never silenced with `[Ignore]`.
