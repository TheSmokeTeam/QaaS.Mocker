using System.Text;
using CommandLine;
using FrameworkHelpTextBuilder = QaaS.Framework.Executions.CommandLineBuilders.HelpTextBuilder;
using LintOptions = QaaS.Mocker.Options.LintOptions;
using RunOptions = QaaS.Mocker.Options.RunOptions;
using TemplateOptions = QaaS.Mocker.Options.TemplateOptions;

namespace QaaS.Mocker.CommandLineBuilders;

/// <summary>
/// Builds top-level and command-specific help text for the QaaS.Mocker CLI.
/// </summary>
public static class HelpTextBuilder
{
    private static readonly string[] CommandHelpSections =
    [
        BuildCommandHelpSection("run"),
        BuildCommandHelpSection("lint"),
        BuildCommandHelpSection("template")
    ];

    /// <summary>
    /// Builds help text for the current parser result and optionally appends the help for every command.
    /// </summary>
    public static string BuildHelpText(Parser cliParser, ParserResult<object> parserResult, bool includeCommandHelp)
    {
        ArgumentNullException.ThrowIfNull(cliParser);
        ArgumentNullException.ThrowIfNull(parserResult);

        var sections = new List<string>
        {
            FrameworkHelpTextBuilder.BuildHelpText(parserResult).ToString().TrimEnd()
        };

        if (includeCommandHelp)
        {
            sections.Add(
                """
                Command Details:
                """.TrimEnd());
            sections.AddRange(CommandHelpSections);
        }

        return string.Join(Environment.NewLine + Environment.NewLine, sections) + Environment.NewLine;
    }

    private static string BuildCommandHelpSection(string commandName)
    {
        using var parser = ParserBuilder.BuildParser();
        var parserResult = parser.ParseArguments<RunOptions, LintOptions, TemplateOptions>([commandName, "--help"]);

        var builder = new StringBuilder();
        builder.AppendLine($"{commandName}:");
        builder.Append(FrameworkHelpTextBuilder.BuildHelpText(parserResult).ToString().TrimEnd());
        return builder.ToString();
    }
}
