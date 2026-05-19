# project_specs.md — QaaS.Mocker.Example

Runnable end-to-end example showcasing:

- A real `.proto` schema with generated client/server code.
- One HTTP server and one gRPC server.
- Sample processors using both `StaticResponseProcessor` and a custom
  user-defined processor.
- A `DataSource` driving response shaping.

## Purpose

- Smoke-validate every release locally (`dotnet run --project
  QaaS.Mocker.Example -- run --configuration-path mocker.qaas.yaml`).
- Provide copy-paste starting points for new users.
- Drive end-to-end integration with `MockerAlphaSmokeApp`.

## What you can change here

- Add new sample processors and stubs as the public API evolves.
- Update the proto schema when the gRPC test surface changes.

## What you must NOT do

- Add production-only processors here — they belong in
  `QaaS.Common.Processors`.
- Reference internal types from `QaaS.Mocker` directly; the example must
  consume only the public package surface.
