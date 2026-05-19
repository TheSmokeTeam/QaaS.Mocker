# CLAUDE.md — QaaS.Mocker.Tests

See `../CLAUDE.md` for solution-wide rules. This file is the working
manual for edits inside the test project for `QaaS.Mocker` (CLI +
composition root + builder).

## Purpose

NUnit + Moq tests for the top-level project: bootstrap wiring, builder
validation, mutual exclusivity, multi-execution orchestration, exit-code
aggregation, CLI path normalisation, and template logic.

## Layout

- `BootstrapTests.cs` — drives `Bootstrap.New(args)` end-to-end against
  captured console output. Covers null/invalid args, `--version`,
  top-level help, verb-level help, and the no-op runner path.
- `MockerTests.cs` — `MockerRunner` orchestration (Task scheduling,
  exit-code aggregation).
- `ExecutionTests/` — single-execution lifecycle (long-running flag,
  cancellation, cleanup).
- `LoadersTests/` — YAML → builder loader behaviour.
- `LogicTests.cs`, `TemplateLogicTests.cs` — fluent sub-builder logic
  (`ServerLogic`, `StubsLogic`, `ControllerLogic`, `TemplateLogic`).
- `CommandLinePathNormalizerTests.cs` — path arg normalisation across
  caller-CWD and app-base-directory.
- `Globals.cs` — shared Serilog `ILogger` (warning-level, NUnit sink) and
  default `Context` with empty `RootConfiguration`.

## Conventions

- `[TestFixture]` + `[Test]` (NUnit). Use `Assert.Multiple` for grouped
  assertions; ordered `Assert.Equal(expected, actual)` semantics apply.
- Capture console via the in-file `CaptureConsoleOut(Action)` helper.
- For tests needing a default `mocker.qaas.yaml`, use
  `WithDefaultConfigurationFileInAppBaseDirectory`.
- Use `Globals.Logger` instead of constructing loggers per test.
- Real filesystem usage stays under `AppContext.BaseDirectory`; clean up
  in `[TearDown]`.

## Mocking conventions

- Moq for any `QaaS.Framework` interface (`IDeserializer`, `ISerializer`,
  `ITransactionProcessor`, etc.).
- Prefer constructing the real `MockerExecutionBuilder` and feeding it
  small in-memory configs over mocking the builder itself.
- Never `[Test(Ignore=...)]` to make a red test green — diagnose instead.

## Run

```bash
dotnet test QaaS.Mocker.Tests/QaaS.Mocker.Tests.csproj --nologo
```

For just one fixture:

```bash
dotnet test QaaS.Mocker.Tests/QaaS.Mocker.Tests.csproj --nologo \
  --filter "FullyQualifiedName~BootstrapTests"
```
