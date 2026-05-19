# project_specs.md — QaaS.Mocker.Stubs

Stub runtime: the 4-stage execution chain that materialises a response
from an incoming request.

## Pipeline (per request)

1. **Deserialize** — bytes → `Data<object>`.
2. **Process** — `ITransactionProcessor.Process(dataSources, request)`.
3. **Serialize** — `Data<object>` → bytes.
4. **Validate** — DataAnnotations on the response shape.

## Key types

| Type | Purpose |
|---|---|
| `TransactionStub` | The runtime stub. Stateless; safe under concurrent invocation. |
| `TransactionStubBuilder` | Fluent partial-class builder (`*Properties`/`*Logic`/`*Validation`). |
| `ProcessorResolution` | Resolves a configured processor name through `QaaS.Framework.Providers` and instantiates with DataAnnotations validation. |
| `ConfigurationObjects/*` | YAML-bound config records for stubs and matchers. |

## Concurrency

- Stub bodies are stateless: every state lives in the request object or
  `Context`. Do not introduce per-stub fields.
- Processor instances may be hot-swapped at runtime via the controller
  command `ChangeActionStub`; the swap is atomic per stub.

## Forbidden in this project

- Direct protocol I/O — that's `QaaS.Mocker.Servers`' job.
- Caching processor outputs across requests (each request must run the
  full pipeline to honour live config changes).
- Returning anything other than `byte[]` from the serialise stage.

## Tests

`QaaS.Mocker.Stubs.Tests` — covers each pipeline stage, hot-swap
semantics, and DataAnnotations validation.
