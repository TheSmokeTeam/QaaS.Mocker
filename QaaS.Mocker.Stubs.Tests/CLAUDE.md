# CLAUDE.md — QaaS.Mocker.Stubs.Tests

See `../CLAUDE.md` for solution-wide rules. This file is the working
manual for edits inside the test project for `QaaS.Mocker.Stubs`.

## Purpose

NUnit + Moq tests for the 4-stage stub pipeline (Deserialize → Process
→ Serialize → Validate), the `TransactionStubBuilder`, and the
`StubFactory`.

## Layout

- `TransactionStubTests.cs` — per-stage behaviour: response pass-through
  when no serialisation is configured, `ArgumentException` when the
  request deserialiser is configured but the body is not `byte[]`,
  serialisation paths, validation failures, and processor exception
  propagation.
- `TransactionStubBuilderTests.cs` — fluent builder validation,
  DataAnnotations enforcement, mutually-exclusive setter rules.
- `StubFactoryTests.cs` — factory wiring of `TransactionStub` from
  configuration objects.
- `Globals.cs` — shared Serilog logger and `Context` fixture.

## Conventions

- `[TestFixture]` + `[Test]` (NUnit).
- Construct stubs inline with `ImmutableList<DataSource>.Empty` when no
  data sources are needed.
- Build `Data<object>` requests with `Body = Encoding.UTF8.GetBytes(...)`
  unless a non-byte body is the test subject.
- Compare `byte[]` results by decoding:
  `Encoding.UTF8.GetString((byte[])result.Body!)`.

## Mocking conventions

- `Mock<IDeserializer>`, `Mock<ISerializer>`, `Mock<ITransactionProcessor>`
  via Moq.
- Helper `CreateProcessor(Func<Data<object>, Data<object>>)` is the
  canonical way to stub a processor — keep using it.
- Verify call counts with `Times.Once`/`Times.Never` rather than asserting
  on internal stub state.

## Forbidden

- `[Test(Ignore=...)]` on red tests.
- Sharing `TransactionStub` instances across tests (each test builds its
  own — the runtime contract is statelessness, but tests should be
  isolated regardless).

## Run

```bash
dotnet test QaaS.Mocker.Stubs.Tests/QaaS.Mocker.Stubs.Tests.csproj --nologo
```
