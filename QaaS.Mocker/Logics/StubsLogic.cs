using System.Collections.Immutable;
using QaaS.Framework.SDK.DataSourceObjects;
using QaaS.Mocker.Stubs;
using QaaS.Mocker.Stubs.Stubs;

namespace QaaS.Mocker.Logics;

public class StubsLogic(StubFactory stubFactory, IImmutableList<DataSource> dataSources)
{
    public IImmutableList<TransactionStub> Build()
    {
        return stubFactory.Build(dataSources);
    }
}
