using CommandLine;

namespace QaaS.Mocker.Options;

/// <summary>
/// Options for the <c>template</c> command.
/// </summary>
[Verb("template",
    HelpText = "Render the effective merged configuration after file, folder, argument, and environment overrides.")]
public sealed record TemplateOptions : MockerOptions
{
    /// <inheritdoc />
    public override ExecutionMode GetExecutionMode() => ExecutionMode.Template;
}
