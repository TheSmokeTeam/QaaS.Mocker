using CommandLine;

namespace QaaS.Mocker.Options;

/// <summary>
/// Options for the <c>lint</c> command.
/// </summary>
[Verb("lint", HelpText = "Validate the configuration and exit without starting runtime listeners.")]
public sealed record LintOptions : MockerOptions
{
    /// <inheritdoc />
    public override ExecutionMode GetExecutionMode() => ExecutionMode.Lint;
}
