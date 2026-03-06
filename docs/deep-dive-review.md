# QaaS.Mocker Deep-Dive Review

## Runtime Flow

`QaaS.Mocker.Example/Program.cs`
- Starts `Bootstrap.New(args).Run()`.

`QaaS.Mocker/Bootstrap.cs`
- Parses CLI arguments and creates a `Mocker` through `MockerLoader`.

`QaaS.Mocker/Loaders/MockerLoader.cs`
- Builds `InternalContext` from the root YAML file, overwrite files, overwrite arguments, and environment-variable resolution.
- Creates `ExecutionBuilder` with the selected execution mode.

`QaaS.Mocker/ExecutionBuilder.cs`
- Validates bound configuration.
- Resolves generator and processor hooks through Autofac.
- Builds data sources, transaction stubs, the selected protocol server, and the optional Redis controller.

`QaaS.Mocker/Execution.cs`
- Dispatches one of three branches:
  - `Run`: server runtime plus optional controller runtime.
  - `Lint`: validation-only branch.
  - `Template`: emits the effective configuration as YAML.

`QaaS.Mocker.Servers`
- `ServerFactory` selects `HttpServer`, `GrpcServer`, or `SocketServer`.
- Each server delegates request-to-stub routing to an `IServerState` implementation.
- `TransactionsCache` is the in-memory persistence layer used by controller consume commands.

`QaaS.Mocker.Controller`
- Builds a Redis-backed controller when both `ServerName` and `Redis` config are present.
- `PingHandler` exposes runtime liveness.
- `CommandHandler` supports `ChangeActionStub`, `Consume`, and `TriggerAction`.

## High-Impact Findings

1. Long-lived server/controller starts were scheduled with `Task.Run`.
- Impact: permanently blocking workloads consumed thread-pool workers, which is a poor fit for HTTP, gRPC, and socket listeners.
- Fix: `Execution` now uses `TaskCreationOptions.LongRunning` for runtime branches.

2. `MockerLoader` leaked its Autofac scope.
- Impact: configuration-builder lifetime scope was never disposed after bootstrap completed.
- Fix: `Bootstrap` now wraps `MockerLoader` in a `using` block and `MockerLoader` implements `IDisposable`.

3. Socket action triggering had a race under concurrent trigger commands.
- Impact: an older timeout could disable an action after a newer trigger had already extended it.
- Fix: `ActionState<TStateIndicator>` now uses versioned activation plus cancellation replacement so the latest trigger window wins.

4. Socket action-to-stub reassignment was missing.
- Impact: `ChangeActionStub` worked for HTTP/gRPC but always failed for socket actions, creating inconsistent controller behavior across transports.
- Fix: `SocketServerState.ChangeActionStub` now resolves and swaps stubs with the same behavior as the other state engines.

5. Cache dequeue logic was not atomic.
- Impact: `TryPeek` followed by `TryDequeue` allowed duplicate/incorrect reads under concurrent consumers.
- Fix: `TransactionsCache` now dequeues atomically with `TryDequeue`.

6. Controller consume used synchronous Redis list pushes inside an async polling loop.
- Impact: unnecessary blocking I/O during consume drains.
- Fix: `CommandHandler.ConsumeAsync` now uses `ListRightPushAsync`.

7. HTTP action reassignment compared action names case-sensitively.
- Impact: controller commands could fail unexpectedly when action name casing differed from configuration casing.
- Fix: `HttpServerState.ChangeActionStub` now uses case-insensitive matching, aligned with gRPC and socket logic.

## Added Regression Coverage

- Overlapping socket trigger commands keep the latest enablement window active.
- Concurrent cache reads dequeue each stored payload exactly once.
- HTTP and socket action-to-stub switching works case-insensitively.
- Socket action-to-stub switching now exercises the swapped stub instead of throwing.
- Controller consume tests validate async Redis push behavior for input-only, output-only, and mixed modes.
