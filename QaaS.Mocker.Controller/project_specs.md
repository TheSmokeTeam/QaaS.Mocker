# project_specs.md — QaaS.Mocker.Controller

Redis-backed control plane that lets a paired `QaaS.Runner` mutate mocker
behaviour at runtime.

## Channels

- `qaas:mocker:{name}:ping`
- `qaas:mocker:{name}:command`
- `qaas:mocker:{name}:command-response`

Channel name construction is delegated to
`Qaas.Mocker.CommunicationObjects.CommunicationMethods`. Always
lowercased for deterministic routing.

## Commands

Defined as DTOs in `Qaas.Mocker.CommunicationObjects`:

| Command | Effect |
|---|---|
| `Ping` | Liveness check. |
| `Status` | Inspect current stub map / counters. |
| `ChangeActionStub` | Hot-swap the processor on a named stub. |
| `TriggerAction` | Fire a `Consume` operation against a configured consumer endpoint. |
| `Consume` | Synchronously pull a buffered message. |

## Key types

- `MockerController` — Redis pub/sub host.
- `CommandHandler` — dispatches commands to per-command handlers.
- `Heartbeat` — publishes Ping responses.

## Conventions

- Use `StackExchange.Redis` `IConnectionMultiplexer` (singleton).
- Every command handler must be idempotent — Redis pub/sub does not
  guarantee exactly-once delivery.

## Forbidden in this project

- Constructing channel names by hand — use `CommunicationMethods`.
- Holding Redis state in process memory longer than necessary; reflect
  authoritative state in the live mocker config.

## Tests

`QaaS.Mocker.Controller.Tests` — covers handler dispatch and command
round-trips. Live Redis tests use `Testcontainers.Redis` or are skipped
based on environment availability (never silenced with `[Ignore]`).
