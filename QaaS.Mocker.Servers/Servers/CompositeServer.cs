using System.Runtime.ExceptionServices;
using Microsoft.Extensions.Logging;
using QaaS.Mocker.Servers.ServerStates;

namespace QaaS.Mocker.Servers.Servers;

/// <summary>
/// Starts multiple transport servers and exposes them as a single runtime to the execution layer.
/// </summary>
public sealed class CompositeServer : IServer
{
    private readonly ILogger _logger;
    private readonly IServer[] _servers;

    public IServerState State { get; init; }

    public CompositeServer(IEnumerable<IServer> servers, ILogger logger)
    {
        _logger = logger;
        _servers = servers.ToArray();
        if (_servers.Length == 0)
            throw new ArgumentException("At least one server is required.", nameof(servers));

        State = new CompositeServerState(_servers.Select(server => server.State));
    }

    public void Start()
    {
        _logger.LogInformation(
            "Starting {ServerCount} server runtime(s) in parallel: {ServerTypes}",
            _servers.Length,
            string.Join(", ", _servers.Select(server => server.GetType().Name)));

        var serverTasks = _servers
            .Select(server => Task.Factory.StartNew(server.Start,
                CancellationToken.None,
                TaskCreationOptions.DenyChildAttach | TaskCreationOptions.LongRunning,
                TaskScheduler.Default))
            .ToArray();

        var completedTaskIndex = Task.WaitAny(serverTasks);
        var completedTask = serverTasks[completedTaskIndex];
        if (completedTask.IsFaulted)
        {
            var flattenedException = completedTask.Exception?.Flatten();
            var exception = flattenedException?.InnerExceptions.FirstOrDefault()
                            ?? (Exception?)flattenedException
                            ?? new InvalidOperationException("A server runtime failed to start.");
            ExceptionDispatchInfo.Capture(exception).Throw();
        }

        throw new InvalidOperationException(
            $"Server runtime '{_servers[completedTaskIndex].GetType().Name}' exited unexpectedly while other transports are still expected to be running.");
    }
}
