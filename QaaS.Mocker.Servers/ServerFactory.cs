using System.Collections.Immutable;
using Microsoft.Extensions.Logging;
using QaaS.Framework.SDK.ContextObjects;
using QaaS.Framework.SDK.DataSourceObjects;
using QaaS.Mocker.Servers.ConfigurationObjects;
using QaaS.Mocker.Servers.Servers;
using QaaS.Mocker.Stubs.Stubs;

namespace QaaS.Mocker.Servers;

/// <summary>
/// Handles the creation of all relevant servers according to given configurations.
/// </summary>
public class ServerFactory
{
    private readonly Context _context;
    private readonly IReadOnlyList<ServerConfig> _servers;

    public ServerFactory(Context context, ServerConfig server) : this(context, [server])
    {
    }

    public ServerFactory(Context context, IReadOnlyList<ServerConfig> servers)
    {
        _context = context;
        _servers = servers;
    }

    public IServer Build(IImmutableList<DataSource> dataSourceList, IImmutableList<TransactionStub> transactionStubList)
    {
        if (_servers.Count == 0)
            throw new ArgumentException("At least one server configuration is required.", nameof(_servers));

        var builtServers = _servers
            .Select(server => BuildSingleServer(server, dataSourceList, transactionStubList))
            .ToArray();

        if (builtServers.Length == 1)
            return builtServers[0];

        _context.Logger.LogInformation(
            "Built {ServerCount} server runtimes from configuration: {ServerTypes}",
            builtServers.Length,
            string.Join(", ", _servers.Select(server => server.ResolveType())));

        return new CompositeServer(builtServers, _context.Logger);
    }

    private IServer BuildSingleServer(ServerConfig server, IImmutableList<DataSource> dataSourceList,
        IImmutableList<TransactionStub> transactionStubList)
    {
        return server.ResolveType() switch
        {
            ServerType.Http => new HttpServer(server.Http!, _context.Logger, transactionStubList),
            ServerType.Grpc => new GrpcServer(server.Grpc!, _context.Logger, transactionStubList),
            ServerType.Socket => new SocketServer(server.Socket!, _context.Logger, transactionStubList,
                dataSourceList),
            _ => throw new ArgumentException("Server type not supported!", nameof(server))
        };
    }
}
