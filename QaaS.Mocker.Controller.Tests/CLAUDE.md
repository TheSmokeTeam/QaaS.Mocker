# CLAUDE.md — QaaS.Mocker.Controller.Tests

See `../CLAUDE.md` for solution-wide rules. This file is the working
manual for edits inside the test project for `QaaS.Mocker.Controller`.

## Purpose

NUnit + Moq tests covering the Redis-backed control plane: `Controller`
lifecycle, `ControllerFactory` wiring, per-command handler dispatch
(`Ping`, `Status`, `ChangeActionStub`, `TriggerAction`, `Consume`), and
the round-trip request/response channel plumbing.

## Layout

- `ControllerTests.cs` — lifecycle and error-propagation tests:
  - `Dispose` disposes the underlying `IConnectionMultiplexer`.
  - Subscription failure on `Start` propagates **before** any sleep/retry.
- `ControllerFactoryTests.cs` — DI/wiring from configuration.
- `HandlersTests/` — per-handler dispatch and serialisation tests for
  `BaseHandler`, `CommandHandler`, `PingHandler`.
- `ConfigurationTests/`, `ExtensionsTests/` — config binding and DI
  extension surface.
- `Globals.cs` — shared Serilog logger.

## Conventions

- `[TestFixture]` + `[Test]` (NUnit).
- Stub all Redis surfaces with Moq:
  - `Mock<IConnectionMultiplexer>` with `GetSubscriber(...)` and
    `GetDatabase(...)` returning mocked `ISubscriber` / `IDatabase`.
  - Set `Configuration` getter for any code path that logs the endpoint.
- For channel-name expectations, compute them through the production
  helpers (`CommunicationMethods.CreateChannel{RunnerToMocker,MockerToRunner}`,
  `BaseHandler.RequestChannel`/`ResponseChannel`). Never literal-string
  match a channel name.
- Test the `ChangeActionStub` hot-swap path through
  `Mock<IServerState>` so production state isn't shared between tests.

## Live Redis

Live integration tests use `Testcontainers.Redis` and are conditionally
run based on environment availability. Tests **must not** be silenced
with `[Ignore]` to mask Redis being absent — gate the fixture instead
(skip via runtime check that surfaces a clear reason).

## Forbidden

- `[Test(Ignore=...)]` to make a red test green.
- Asserting on hard-coded channel strings.
- Real Redis broker dependency in unit fixtures (only in the
  Testcontainers-backed integration set).

## Run

```bash
dotnet test QaaS.Mocker.Controller.Tests/QaaS.Mocker.Controller.Tests.csproj --nologo
```
