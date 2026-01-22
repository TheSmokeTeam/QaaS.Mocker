using Autofac;
using QaaS.Framework.SDK.ContextObjects;

namespace QaaS.Mocker.Executions;

/// <summary>
/// Lint execution mode.
/// Shows if the configuration/configured providers are valid.
/// </summary>
public class LintExecution(Context context) : BaseExecution(context, true)
{
    /// <inheritdoc />
    protected override int Execute(ILifetimeScope scope) => 0;
}