# CLAUDE.md — QaaS.Mocker (CLI + composition root)

See `../CLAUDE.md` for solution-wide rules and `project_specs.md` for the
authoritative spec. This file is the working manual for edits inside this
project only.

## Purpose

Top-level project: CLI entry, YAML loading, Autofac composition, the root
fluent builder (`MockerExecutionBuilder`), and the multi-execution
orchestrator. Owns the validation framework that gates every `Build()`.

## Key files

- `Bootstrap.cs` — static `Bootstrap.New(args)` factory; parses CommandLine
  options, builds the Autofac container, returns a `MockerRunner`.
- `MockerRunner.cs` — schedules each `Execution` via `Task.Run`,
  aggregates exit codes. No long-running work lives here.
- `Execution.cs` — single-execution lifecycle; long-running scheduling.
- `ExecutionBuilder.cs` (~900 LoC) — root fluent builder, mutual-exclusivity
  enforcement (`Server` xor `Servers`, ~lines 793-798), cross-property
  validation.
- `IExecutionBuilderConfigurator.cs` — code-only host hook (used by
  `QaaS.Mocker.Example`'s `Program.cs`).
- `Loaders/MockerLoader.cs`, `Loaders/ExecutionBuilderConfiguratorLoader.cs`
  — YAML → builder bridge.
- `Logics/{ServerLogic,StubsLogic,ControllerLogic,TemplateLogic}.cs` —
  fluent sub-builders surfaced through the root builder.
- `CommandLineBuilders/`, `Options/` — CommandLine verb definitions
  (`run`, `template`).
- `CommandLinePathNormalizer.cs` — normalises `--configuration-path` and
  related path args against caller CWD vs. app base directory.

## Conventions

- All cross-property validation lives in `ExecutionBuilder.Validate` (or
  on the dedicated `ValidationAttribute`s). Never inline silent fallbacks.
- `Server` and `Servers` are mutually exclusive at builder level — pick
  one shape only.
- Long-running task scheduling belongs in `Execution`, never in
  `MockerRunner` (it only aggregates).
- Each `Execution` gets its own Autofac scope; do not share scope across
  executions.
- Path arguments must be funneled through `CommandLinePathNormalizer` so
  hosted (`Example`) and library use both work.

## Forbidden in this project

- Mutating builder state after `Build()` returns.
- Configuring both `Server` and `Servers` on the same builder.
- Adding long-running work to `MockerRunner`.
- Bypassing validation with `null!` or `[ValidationException]` suppression.
- Hard-coding YAML keys outside the loader.
- Direct protocol I/O — that lives in `QaaS.Mocker.Servers`.

## Tests

Project: `QaaS.Mocker.Tests` (NUnit + Moq).

```bash
dotnet test QaaS.Mocker.Tests/QaaS.Mocker.Tests.csproj --nologo
```

Covers: bootstrap wiring, builder validation, `Server`/`Servers` mutual
exclusivity, multi-execution orchestration, exit-code aggregation, CLI
path normalisation, template logic.
