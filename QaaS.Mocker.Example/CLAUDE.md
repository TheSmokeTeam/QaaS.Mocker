# CLAUDE.md — QaaS.Mocker.Example

See `../CLAUDE.md` for solution-wide rules and `project_specs.md` for the
spec. This file is the working manual for edits inside this project.

## Purpose

Runnable end-to-end example. Hosts an HTTP and a gRPC server, a real
`.proto` schema, demo processors, and a `DataSource` driving response
shaping. Used as a smoke test for releases and as a copy-paste starting
point for new users.

## Key files

- `Program.cs` — top-level statements: normalises CLI paths via
  `CommandLinePathNormalizer.Normalize`, switches CWD to the app base
  directory, then `Bootstrap.New(args).Run()`.
- `mocker.qaas.yaml`, `mocker.grpc.qaas.yaml`, `mocker.runner.qaas.yaml`
  — sample configurations.
- `Protos/` — `.proto` files compiled into the example.
- `Processors/` — sample `ITransactionProcessor` implementations
  (alongside `StaticResponseProcessor` from common packages).
- `Data/`, `SocketData/`, `Variables/`, `Chart/`, `Generators/`,
  `Postman/` — fixture inputs feeding the YAML config.
- `Certificates/` — example dev TLS material; **not** for production.

## Conventions

- Consume only the public package surface of `QaaS.Mocker` — no
  references to internal types.
- Run from the example directory:

  ```bash
  dotnet run --project QaaS.Mocker.Example -- run --configuration-path mocker.qaas.yaml
  ```

- When adding a sample processor, mirror the YAML wiring in one of the
  three sample configs so it's discoverable.

## Forbidden in this project

- Adding production-only processors here — those belong in
  `QaaS.Common.Processors`.
- Referencing internal types from `QaaS.Mocker` directly.
- Shipping the example certificates as a production artefact.
- Hard-coding absolute paths in YAML; use the relative paths the
  normaliser handles.

## Tests

This project has no dedicated test assembly — it serves as an executable
smoke test. Verify by running the example with each sample YAML and
exercising it via `MockerAlphaSmokeApp` or curl/grpcurl. Solution-level
tests live in the four `*.Tests` projects.
