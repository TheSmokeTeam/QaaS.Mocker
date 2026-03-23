using CommandLine;

namespace QaaS.Mocker.Options;

/// <summary>
/// Options for the <c>run</c> command.
/// </summary>
[Verb("run", HelpText = "Start the configured mock servers and optional controller runtime.")]
public sealed record RunOptions : MockerOptions
{
    /// <inheritdoc />
    public override ExecutionMode GetExecutionMode() => ExecutionMode.Run;
}
