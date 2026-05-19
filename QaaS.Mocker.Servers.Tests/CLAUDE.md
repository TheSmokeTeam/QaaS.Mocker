# CLAUDE.md — QaaS.Mocker.Servers.Tests

See `../CLAUDE.md` for solution-wide rules. This file is the working
manual for edits inside the test project for `QaaS.Mocker.Servers`.

## Purpose

NUnit + Moq tests covering each per-protocol server (HTTP, gRPC, Socket,
Composite), routing/match-rule evaluation, server lifecycle reconciliation,
and the supporting Actions/Caches/State infrastructure.

## Layout

- `HttpServerTests.cs` — drives `HandleTransactionAsync` (invoked via
  reflection) against a `DefaultHttpContext` with `MemoryStream` request/
  response bodies. Asserts status code and payload bytes.
- `GrpcServerTests.cs` — gRPC dispatch, including the proto-driven
  service surface.
- `SocketServerTests.cs` — broadcast and collect modes against loopback.
- `CompositeServerTests.cs` — multi-server composition.
- `ServerFactoryTests.cs` — factory wiring from configuration.
- `ActionsTests/`, `CachesTests/`, `ConfigurationTests/`,
  `ExtensionsTests/`, `ServerStateTests/` — supporting infrastructure.
- `Globals.cs` — shared Serilog logger fixture.

## Conventions

- `[TestFixture]` + `[Test]` (NUnit).
- HTTP tests: build a `DefaultHttpContext`, set `Request.Method/Scheme/
  Host/Path/Body` and `Response.Body = new MemoryStream()`, then
  read-back via `StreamReader` after rewinding `Position = 0`.
- Stub fixtures use `ImmutableList<DataSource>.Empty` and a real or
  mocked `ITransactionProcessor`.
- For TLS, use the test certificates under
  `System.Security.Cryptography.X509Certificates`; never reference the
  example's certs.
- Sockets/gRPC bind to `127.0.0.1:0` (ephemeral port) when possible to
  avoid CI port collisions.

## Mocking conventions

- Moq for `ITransactionProcessor`, `IDeserializer`, `ISerializer`, and
  any `IHostApplicationLifetime`/Kestrel infra you need to stub.
- Reflection (`MethodInfo.Invoke`) is acceptable for invoking internal
  request handlers in HTTP/gRPC tests, but only when no public surface
  is reasonable.

## Forbidden

- `[Test(Ignore=...)]` on red tests.
- Hard-coding fixed ports (causes flake on shared CI).
- Real network calls outside loopback.

## Run

```bash
dotnet test QaaS.Mocker.Servers.Tests/QaaS.Mocker.Servers.Tests.csproj --nologo
```
