# QaaS.Mocker

`QaaS.Mocker` runs configurable mock servers for QaaS workloads.

Supported protocols:
- `Http` / `Https`
- `Grpc` / `Grpcs`
- `Socket`
- Optional Redis controller API (ping + runtime commands)

## Project Flow

1. `Bootstrap.New(args)` parses CLI options into `MockerOptions`.
2. `MockerLoader` builds `InternalContext` from YAML + overwrite files/arguments + env variables.
3. `ExecutionBuilder` validates config, loads hooks, builds:
   - Data sources
   - Transaction stubs
   - Server implementation (`HttpServer`, `GrpcServer`, or `SocketServer`)
   - Optional controller
4. `Execution.Start()` runs one mode:
   - `Run`: starts server (and controller when configured)
   - `Lint`: validates configuration/build path
   - `Template`: prints or writes a generated template YAML
5. Server state objects route incoming actions/RPCs/messages to transaction stubs and cache input/output for controller consume commands.

Hook loading uses QaaS provider modules (`HooksLoaderModule<T>`) and resolves hooks from loaded assemblies.
This keeps processor integrations pluggable: you can add processors from YAML or by code configuration as long as
the assembly containing those `ITransactionProcessor` implementations is available at runtime.

## Solution Layout

- `QaaS.Mocker`: CLI, bootstrap, execution orchestration
- `QaaS.Mocker.Stubs`: stub factory + transaction stub execution
- `QaaS.Mocker.Servers`: HTTP/gRPC/socket servers and state routing
- `QaaS.Mocker.Controller`: Redis control handlers
- `QaaS.Mocker.Example`: runnable example configs/processors
- `QaaS.Mocker.*.Tests`: NUnit unit tests

## Code Configuration

`ExecutionBuilder` supports code-first CRUD operations:
- Data sources: `CreateDataSource`, `ReadDataSource`, `UpdateDataSource`, `DeleteDataSource`
- Stubs: `CreateStub`, `ReadStub`, `UpdateStub`, `DeleteStub`
- Server/controller: `Read*`, `Update*`, `Replace*`, `DeleteController`

`TransactionStubBuilder` supports configuring processor hook names and configuration objects in code (`Configure(object)`).

## Prerequisites

- .NET SDK `10.0.x`
- Docker Desktop (optional, for image build/run)
- `dotnet dev-certs` (included with .NET SDK) for local TLS example

## Build And Test

```bash
dotnet restore QaaS.Mocker.sln
dotnet build QaaS.Mocker.sln -c Release -warnaserror
dotnet test QaaS.Mocker.sln -c Release --no-build
```

## Run Example Without Any Script

No script is required.

1. Generate a dev certificate file expected by example YAML:

```bash
dotnet dev-certs https -ep QaaS.Mocker.Example/Certificates/devcert.pfx -p qaas-dev-cert
```

2. Trust the certificate (local machine):

```bash
dotnet dev-certs https --trust
```

3. Run HTTPS example:

```bash
cd QaaS.Mocker.Example
dotnet run --project . -- mocker.qaas.yaml
```

4. Verify endpoint:

```bash
curl -k https://localhost:8443/health
```

## Run gRPC Example

```bash
cd QaaS.Mocker.Example
dotnet run --project . -- mocker.grpc.qaas.yaml
```

The gRPC service listens on `localhost:50051` with TLS and exposes `EchoService/Echo`.

## Build And Run Docker Image

1. Ensure `QaaS.Mocker.Example/Certificates/devcert.pfx` exists (see cert generation command above).
2. Build image:

```bash
docker build -t qaas-mocker-example .
```

3. Run image (default entrypoint uses `mocker.qaas.yaml`):

```bash
docker run --rm -p 8443:8443 qaas-mocker-example
```

## CI / Publish

Workflow: `.github/workflows/ci.yml`

- Restores, builds with `-warnaserror`, and runs tests
- Uses workflow concurrency to cancel duplicate branch/PR runs in progress
- On tags: validates SemVer, packs `QaaS.Mocker`, pushes NuGet package
