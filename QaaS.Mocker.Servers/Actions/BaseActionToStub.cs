namespace QaaS.Mocker.Servers.Actions;

public abstract class BaseActionToStub<TStub>
{
    public string? ActionName { get; set; }
    public TStub Stub { get; set; }
}