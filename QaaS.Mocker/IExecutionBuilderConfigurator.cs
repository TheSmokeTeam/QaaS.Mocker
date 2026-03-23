namespace QaaS.Mocker;

/// <summary>
/// Applies code-based execution configuration to a mocker execution builder.
/// </summary>
public interface IExecutionBuilderConfigurator
{
    /// <summary>
    /// Mutates the given execution builder before the mocker runner starts.
    /// </summary>
    /// <param name="executionBuilder">The execution builder to configure.</param>
    void Configure(ExecutionBuilder executionBuilder);
}
