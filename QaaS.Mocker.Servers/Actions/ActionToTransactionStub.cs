using QaaS.Mocker.Stubs.Stubs;

namespace QaaS.Mocker.Servers.Actions;

/// <summary>
/// Maps a server action to the transaction stub that should execute it.
/// </summary>
public class ActionToTransactionStub : BaseActionToStub<TransactionStub>;
