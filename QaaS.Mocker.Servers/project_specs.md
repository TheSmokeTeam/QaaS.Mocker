# project_specs.md — QaaS.Mocker.Servers

Per-protocol server runtimes that route incoming traffic into the stub
pipeline (`QaaS.Mocker.Stubs`).

## Hosted protocols

| Server | Underlying tech | Notes |
|---|---|---|
| HTTP / HTTPS | Kestrel (`Microsoft.AspNetCore.Server.Kestrel`) | Path/method/header matching; supports any content-type. |
| gRPC / gRPCs | `Grpc.AspNetCore` | Schema is supplied by the user via a proto file; mocker hosts the generated services. |
| Socket broadcast | raw `System.Net.Sockets` | Pushes synthesised data to connected clients. |
| Socket collect | raw `System.Net.Sockets` | Records incoming frames into a buffer for assertion. |
| Kafka / RabbitMQ stubs | Framework protocol drivers | Acts as a producer or consumer endpoint that triggers stubs. |

## Lifecycle

- Each server is reconciled to its owning `Execution`. Start/stop is
  idempotent.
- Multiple executions may host servers on different ports; cross-execution
  port conflicts are rejected at `Build()` time.

## Key types

- `*Server` — concrete server class per protocol.
- `*ServerBuilder` — fluent partial-class builder.
- `Routing/*` — match-rule evaluators.

## Concurrency

- Each server is responsible for its own thread/async safety. Stubs are
  stateless, so the only shared mutation point is the live processor map
  (managed by `QaaS.Mocker.Stubs`).

## Forbidden in this project

- Putting business logic in the server (delegate to stubs).
- Blocking the request thread on long-running async work — use proper
  async/await throughout.
- Hard-coding addresses; always read from configuration.

## Tests

`QaaS.Mocker.Servers.Tests` — covers routing, stub dispatch, and
lifecycle reconciliation per protocol.
