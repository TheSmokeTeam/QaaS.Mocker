# CLAUDE.md — QaaS.Mocker.Servers

See `../CLAUDE.md` for solution-wide rules and `project_specs.md` for the
spec. This file is the working manual for edits inside this project.

## Purpose

Per-protocol server runtimes that bind a listening endpoint, match
incoming traffic against configured rules, and dispatch into the stub
pipeline (`QaaS.Mocker.Stubs`). Each server is reconciled to the owning
`Execution` and is responsible for its own thread/async safety.

## Key files

- `Servers/IServer.cs` — server contract.
- `Servers/HttpServer.cs` — Kestrel-hosted HTTP/HTTPS; routes by
  path/method/header into a configured `TransactionStub`.
- `Servers/GrpcServer.cs` — `Grpc.AspNetCore`-hosted gRPC/gRPCs; user
  supplies the `.proto`, mocker hosts the generated services.
- `Servers/SocketServer.cs` — raw `System.Net.Sockets` for broadcast
  (push) / collect (record) modes.
- `Servers/CompositeServer.cs` — composes multiple servers under a single
  Execution.
- `ServerFactory.cs` — builds servers from configuration.
- `ConfigurationObjects/` — YAML-bound config records (incl.
  `HttpServerConfigs/HttpMethod`, gRPC and socket variants).
- `Actions/`, `Caches/`, `ServerStates/`, `Extensions/` — supporting
  infra: action wiring, response caches, server lifecycle state machine,
  helpers.

## Conventions

- Servers do dispatch only; **all** business logic belongs in stubs.
- Async-all-the-way through the request path (Kestrel/gRPC); never block
  on `.Result`/`.Wait()`.
- Read addresses, ports, and TLS material from configuration — never
  hard-code.
- Start/stop must be idempotent. Cross-execution port conflicts are
  rejected at builder `Build()` time.
- The only mutable shared state is the live processor map (owned by
  `QaaS.Mocker.Stubs`); servers do not cache stub responses.

## Forbidden in this project

- Putting business logic in the server (delegate to stubs).
- Blocking the request thread on long-running async work.
- Hard-coding addresses, ports, or TLS paths.
- `new HttpClient()` per call — use `IHttpClientFactory`.
- Sharing server instances across executions.
- Returning anything other than `byte[]` to the wire from the serialise
  stage.

## Tests

Project: `QaaS.Mocker.Servers.Tests` (NUnit + Moq).

```bash
dotnet test QaaS.Mocker.Servers.Tests/QaaS.Mocker.Servers.Tests.csproj --nologo
```

Layout: one fixture per server (`HttpServerTests`, `GrpcServerTests`,
`SocketServerTests`, `CompositeServerTests`), plus `ServerFactoryTests`,
`ActionsTests/`, `CachesTests/`, `ConfigurationTests/`, `ExtensionsTests/`,
`ServerStateTests/`, and the shared `Globals.cs` (`Serilog` logger).

HTTP tests drive `DefaultHttpContext` directly with `MemoryStream` request/
response bodies and call `HandleTransactionAsync` via reflection. Stubs
under test are built inline with `ImmutableList<DataSource>.Empty` and a
real or mocked `ITransactionProcessor`. gRPC/socket tests use loopback.
