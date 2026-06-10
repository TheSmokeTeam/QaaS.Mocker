Read `AGENTS.md` at the repo root first — it contains the YAML config schema, Redis control-plane
channel conventions, CLI flags, and the critical coupling to `Qaas.Mocker.CommunicationObjects`.

## Essentials
- **TFM**: net10.0; C# nullable + ImplicitUsings enabled.
- **Build**: `dotnet build -m QaaS.Mocker.sln`.
- **Test**: `dotnet test QaaS.Mocker.sln`.
- **Canonical run**: `dotnet run --project QaaS.Mocker -- run mocker.qaas.yaml`.
- **Hook type**: only `ITransactionProcessor` (from QaaS.Framework.SDK) — implement in an
  assembly visible to Framework.Providers scan order (`QaaS.*` → `Common.*` → user libs).
- **Redis channels are lowercase**: `runner-to-mocker:{contentType}:{serverName}` /
  `mocker-to-runner:{contentType}:{serverName}` — case mismatch causes silent misses.
- **DTOs** (`ChangeActionStub`, `TriggerAction`, `Consume`, `Ping`, `Status`) live in
  `Qaas.Mocker.CommunicationObjects` (Tier-1 repo, note lowercase 'aas') — keep in sync.
- **Commits**: conventional style (`feat:`, `fix:`); run `dotnet format` before committing.
