# QaaS.Mocker

Configurable mock runtime for QaaS protocol workloads.

[![CI](https://img.shields.io/badge/CI-GitHub_Actions-2088FF)](./.github/workflows/ci.yml)
[![Docs](https://img.shields.io/badge/docs-qaas--docs-blue)](https://thesmoketeam.github.io/qaas-docs/)
[![.NET](https://img.shields.io/badge/.NET-10.0-512BD4)](https://dotnet.microsoft.com/)

## Contents
- [Overview](#overview)
- [Packages](#packages)
- [Functionalities](#functionalities)
- [Protocol Support](#protocol-support)
- [Quick Start](#quick-start)
- [Run the Example](#run-the-example)
- [Build and Test](#build-and-test)
- [Documentation](#documentation)

## Overview
This repository contains one solution: [`QaaS.Mocker.sln`](./QaaS.Mocker.sln).

`QaaS.Mocker` is published as a single NuGet package and composes internal runtime modules for stubs, protocol servers, and optional Redis controller operations.

## Packages
| Package | Latest Version | Total Downloads |
|---|---|---|
| [QaaS.Mocker](https://www.nuget.org/packages/QaaS.Mocker/) | [![NuGet](https://img.shields.io/nuget/v/QaaS.Mocker?logo=nuget)](https://www.nuget.org/packages/QaaS.Mocker/) | [![Downloads](https://img.shields.io/nuget/dt/QaaS.Mocker?logo=nuget)](https://www.nuget.org/packages/QaaS.Mocker/) |

## Functionalities
### [QaaS.Mocker](./QaaS.Mocker/)
- Parses CLI options and loads YAML configuration with overwrite files/arguments and optional environment resolution.
- Builds and validates execution context, data sources, stubs, server runtime, and optional controller runtime.
- Supports execution modes: `Run`, `Lint`, and `Template`.

### [QaaS.Mocker.Stubs](./QaaS.Mocker.Stubs/)
- Builds transaction stubs from configured processors and data source bindings.
- Executes request/response transformation through configurable serializer/deserializer pairs.
- Includes default status-code stubs for not-found and internal-error flows.

### [QaaS.Mocker.Servers](./QaaS.Mocker.Servers/)
- Hosts protocol runtimes for `HTTP/HTTPS`, `gRPC/gRPCs`, and `Socket`.
- Routes actions/RPC methods to transaction stubs through state objects.
- Caches input/output payloads for controller consume operations.

### [QaaS.Mocker.Controller](./QaaS.Mocker.Controller/)
- Initializes optional Redis-backed runtime controller when configured.
- Handles `Ping` and `Command` channels.
- Supports runtime commands such as action-to-stub switch, trigger, and consume.

### [QaaS.Mocker.Example](./QaaS.Mocker.Example/)
- Provides runnable HTTP and gRPC mocker configurations, sample processors, protobuf schema, and sample data assets.

## Protocol Support
Supported protocol/runtime families in `QaaS.Mocker`:

| Family | Implementations |
|---|---|
| HTTP / RPC | HTTP, HTTPS, gRPC, gRPCs |
| Streaming / Socket | Socket (broadcast and collect modes) |
| Runtime Control | Redis-backed controller channels (ping and command) |

## Quick Start
Install package:

```bash
dotnet add package QaaS.Mocker
```

Upgrade package:

```bash
dotnet add package QaaS.Mocker --version <target-version>
dotnet restore
```

## Run the Example
The example uses relative paths for certificates, sample data, and socket payloads, so run it from [`QaaS.Mocker.Example`](./QaaS.Mocker.Example/).

1. Open a terminal in the example directory.

```powershell
Set-Location .\QaaS.Mocker.Example
```

2. Create the development certificate expected by the sample HTTPS and gRPC configs.

```powershell
dotnet dev-certs https -ep .\Certificates\devcert.pfx -p qaas-dev-cert
```

3. Lint the HTTP sample before starting it.

```powershell
dotnet run -- --mode Lint mocker.qaas.yaml
```

4. Start the HTTP sample.

```powershell
dotnet run -- mocker.qaas.yaml
```

5. In a second terminal, call the sample endpoint.

```powershell
curl.exe -k https://127.0.0.1:8443/health
```

6. Lint and start the gRPC sample in the same way when you want to test the gRPC configuration.

```powershell
dotnet run -- --mode Lint mocker.grpc.qaas.yaml
dotnet run -- mocker.grpc.qaas.yaml
```

7. If you have [`grpcurl`](https://github.com/fullstorydev/grpcurl) installed, you can call the gRPC sample from Git Bash, WSL, or another POSIX-style shell with:

```bash
grpcurl -insecure -import-path Protos -proto echo.proto -d '{"message":"hello"}' 127.0.0.1:50051 qaas.mocker.example.EchoService/Echo
```

## Build and Test
```bash
dotnet restore QaaS.Mocker.sln
dotnet build QaaS.Mocker.sln -c Release --no-restore
dotnet test QaaS.Mocker.sln -c Release --no-build
```

## Documentation
- Official docs: [thesmoketeam.github.io/qaas-docs](https://thesmoketeam.github.io/qaas-docs/)
- CI workflow: [`.github/workflows/ci.yml`](./.github/workflows/ci.yml)
- NuGet package: [QaaS.Mocker on NuGet](https://www.nuget.org/packages/QaaS.Mocker/)
