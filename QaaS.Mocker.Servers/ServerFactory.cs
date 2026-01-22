using System.Collections.Immutable;
using QaaS.Framework.SDK.ContextObjects;
using QaaS.Framework.SDK.DataSourceObjects;
using QaaS.Mocker.Servers.ConfigurationObjects;
using QaaS.Mocker.Servers.Servers;
using QaaS.Mocker.Stubs.Stubs;

namespace QaaS.Mocker.Servers;

/// <summary>
/// Handles the creation of all relevant servers according to given configurations.
/// </summary>
public class ServerFactory(Context context, ServerConfig server)
{
    public IServer Build(IImmutableList<DataSource> dataSourceList, IImmutableList<TransactionStub> transactionStubList)
    {
        return server.Type switch
        {
            ServerType.Http => new HttpServer(server.Http!, context.Logger, transactionStubList),
            ServerType.Grpc => throw new NotImplementedException(),
            ServerType.Socket => new SocketServer(server.Socket!, context.Logger, transactionStubList,
                dataSourceList),
            _ => throw new ArgumentException("Server type not supported!", server.Type.ToString())
        };
    }
}