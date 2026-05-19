# project_specs.md — QaaS.Mocker (CLI + Bootstrap)

Top-level project of the Mocker solution. Owns the CLI entrypoint, YAML
loader, and the central composition root `MockerExecutionBuilder`.

## Key types

| Type | File | Purpose |
|---|---|---|
| `Bootstrap` | `Bootstrap.cs` | Static `New(args)` factory. Builds Autofac container, wires modules, returns a `MockerRunner`. |
| `MockerRunner` | `MockerRunner.cs` | Schedules each Execution via `Task.Run`, aggregates exit codes. |
| `Execution` | `Execution.cs` | Single-execution lifecycle; long-running task scheduling. |
| `MockerExecutionBuilder` | `ExecutionBuilder.cs` (~900 LoC) | Root fluent builder + validation. Mutually-exclusive `Server` / `Servers`. |
| Validation framework | `Validation/` | Custom `ValidationAttribute`s for cross-property rules (data sources exist, processors exist, server/servers mutual-exclusivity, etc.). |

## CLI commands

`run`, `template` — defined under `Options/`. `run` blocks until all
configured servers shut down (or SIGINT).

## Conventions

- Mutual exclusivity (`Server` xor `Servers`) is enforced in
  `ExecutionBuilder.Validate` (~lines 793-798) — never bypass.
- Server reconciliation: each Execution owns its servers, never share
  across executions.
- Long-running scheduling lives here, not in `MockerRunner`.

## Tests

`QaaS.Mocker.Tests` covers: bootstrap wiring, builder validation, mutual
exclusivity, multi-execution orchestration, exit-code aggregation.
