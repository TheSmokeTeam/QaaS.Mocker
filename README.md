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
