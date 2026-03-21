using System.Collections.Immutable;
using QaaS.Framework.SDK.DataSourceObjects;
using QaaS.Mocker.Stubs;
using QaaS.Mocker.Stubs.Stubs;

namespace QaaS.Mocker.Logics;

/// <summary>
/// Builds transaction stubs after the data-source graph has been resolved.
/// </summary>
public class StubsLogic(StubFactory stubFactory, IImmutableList<DataSource> dataSources)
{
    /// <summary>
    /// Creates the immutable stub list for the runtime.
    /// </summary>
    public IImmutableList<TransactionStub> Build()
    {
        return stubFactory.Build(dataSources);
    }
}
