# CLAUDE.md — QaaS.Mocker.Stubs

See `../CLAUDE.md` for solution-wide rules and `project_specs.md` for the
spec. This file is the working manual for edits inside this project.

## Purpose

The stub runtime. A `TransactionStub` is the stateless 4-stage pipeline
that maps an incoming request `Data<object>` to a response `Data<object>`:
**Deserialize → Process → Serialize → Validate**. Wired through
`StubFactory` and configured via the YAML-bound types under
`ConfigurationObjects/`.

## Key files

- `Stubs/TransactionStub.cs` — the runtime stub; `Exercise(Data<object>)`
  drives the 4 stages. Stateless; safe under concurrent invocation.
- `StubFactory.cs` — builds `TransactionStub` instances from configuration.
- `Processors/` — processor resolution helpers (`ITransactionProcessor`
  is discovered through `QaaS.Framework.Providers`; instantiation runs
  DataAnnotations validation).
- `ConfigurationObjects/` — YAML-bound config records for stubs/matchers.
- `Constants.cs` — string keys shared across stub config.

## Conventions

- Stub bodies are stateless; per-request state lives on the request
  `Data<object>` or `Context`. Do not introduce per-stub mutable fields.
- Body conversion: when a `RequestBodyDeserializer` is configured the
  stub asserts `Body` is `byte[]` and throws `ArgumentException` otherwise
  (`TransactionStub.Exercise`). Honour that contract.
- Processor instances may be hot-swapped at runtime via the controller
  command `ChangeActionStub`; the swap is atomic per stub.
- Every request runs the full pipeline — never cache processor outputs
  across requests.

## Forbidden in this project

- Direct protocol I/O — that's `QaaS.Mocker.Servers`' job.
- Returning anything other than `byte[]` from the serialise stage.
- Caching processor results between requests.
- Per-stub mutable fields or static state for request handling.
- Direct `new TransactionStub { ... }` outside tests/factories — production
  code goes through the builder/factory.

## Tests

Project: `QaaS.Mocker.Stubs.Tests` (NUnit + Moq).

```bash
dotnet test QaaS.Mocker.Stubs.Tests/QaaS.Mocker.Stubs.Tests.csproj --nologo
```

Layout:
- `TransactionStubTests.cs` — pipeline stage behaviour (deserialize/
  serialize/validate/error paths). Mocks `IDeserializer`/`ISerializer` via
  Moq; stubs `ITransactionProcessor` through a small `CreateProcessor`
  helper.
- `TransactionStubBuilderTests.cs` — fluent builder + DataAnnotations.
- `StubFactoryTests.cs` — factory wiring.
- `Globals.cs` — shared Serilog `ILogger` and `Context` fixture.

Mocking convention: prefer `Mock<T>.Object` for `IDeserializer`/
`ISerializer`/`ITransactionProcessor`. Use `ImmutableList<DataSource>.Empty`
where a stub needs no data sources. Assertions use `Assert.That(actual,
Is.EqualTo(expected))` and `Assert.Throws<TException>(...)`.
