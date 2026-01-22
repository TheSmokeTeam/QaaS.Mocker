using QaaS.Framework.Executions;
using QaaS.Framework.SDK.ContextObjects;
using QaaS.Mocker.Options;

namespace QaaS.Mocker;

public class Execution(ExecutionMode executionMode, Context context, bool runLocally) : BaseExecution
{
   
    public override int Start()
    {
        throw new NotImplementedException();
    }

    public override void Dispose()
    {
    }
}