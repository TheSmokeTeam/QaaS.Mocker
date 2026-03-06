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

8. Socket collect endpoints started disabled and never self-activated.
- Impact: socket collect mocks could hang indefinitely unless an external trigger command arrived first, which contradicts the documented request/response usage flow.
- Fix: socket collect actions now default to enabled, while trigger timeouts restore each action to its configured baseline state instead of always forcing `false`.

9. Socket collect buffered the configured capacity, not the received payload length.
- Impact: TCP and UDP payloads were padded with trailing zero bytes, corrupting text protocols, binary framing, and request deserialization.
- Fix: socket reads now honor the actual byte count returned by `Receive`/`ReceiveFrom`.

10. UDP broadcast was configured but not actually transmittable.
- Impact: broadcast actions on UDP endpoints had no remote destination and would fail at runtime.
- Fix: UDP broadcast is now rejected during validation and guarded again in `SocketServer` construction.

11. Controller and stub deserialization were too narrow for real runner traffic.
- Impact: camelCase request payloads, string enum values, and gRPC protobuf request bodies could fail to deserialize even though they are valid transport representations.
- Fix: controller handlers now deserialize case-insensitively and accept string enums, and transaction stubs now support protobuf/message-backed request bodies.

12. Consume acknowledged success before the drain actually succeeded.
- Impact: Runner could receive `Succeeded` even when Redis queue pushes later failed, masking lost consume data and making retries unreliable.
- Fix: `CommandHandler` now completes the consume lifecycle before returning success and surfaces push failures in the command response.

13. Socket runtime scheduling and teardown had protocol-specific defects.
- Impact: accepted TCP channels leaked, TCP broadcasts could truncate on partial sends, UDP endpoints could overlap processing on the same socket, and UDP startup applied TCP-only socket options (`NoDelay`, `LingerState`) that crash on Windows.
- Fix: accepted client channels are now disposed, broadcast writes loop until the full payload is sent, UDP endpoint scheduling is single-flight, and TCP-only socket options are applied only to TCP sockets.

14. Socket protocol/type mismatches failed late with opaque platform exceptions.
- Impact: a UDP endpoint left on the default `SocketType.Stream` would crash inside `System.Net.Sockets` instead of failing validation with a clear message.
- Fix: socket endpoint validation now enforces protocol/socket-type compatibility, and `SocketServer` has matching constructor guards.

## Added Regression Coverage

- Overlapping socket trigger commands keep the latest enablement window active.
- Concurrent cache reads dequeue each stored payload exactly once.
- HTTP and socket action-to-stub switching works case-insensitively.
- Socket action-to-stub switching now exercises the swapped stub instead of throwing.
- Controller consume tests validate async Redis push behavior for input-only, output-only, and mixed modes.
- Controller handler tests cover camelCase payloads and string enum request values.
- Transaction stubs cover protobuf request-body deserialization.
- Socket extensions cover exact-length TCP and UDP reads.
- Socket server tests cover UDP broadcast rejection, protocol/socket-type validation, partial-send retries, zero-send failures, and single-flight UDP scheduling.
