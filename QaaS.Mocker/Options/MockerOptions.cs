using System.ComponentModel.DataAnnotations;
using CommandLine;
using QaaS.Framework.Configurations.CustomValidationAttributes;
using QaaS.Framework.Executions.Options;

namespace QaaS.Mocker.Options;

/// <summary>
/// Mocker running options.
/// </summary>
public record MockerOptions : LoggerOptions
{
    [Option('m', "mode",
        HelpText = $"""
                    The mocker execution mode. Overrides the default execution mode 
                    ({nameof(Options.ExecutionMode.Run)}). All available options (not case sensitive) are:
                    {nameof(Options.ExecutionMode.Run)}, {nameof(Options.ExecutionMode.Lint)}, 
                    {nameof(Options.ExecutionMode.Template)}
                    """)]
    public ExecutionMode? ExecutionMode { get; init; } = Options.ExecutionMode.Run;

    [Required, ValidPath, Value(0, Default = Constants.DefaultMockerConfigurationFileName,
         HelpText = """
                    Path to a mocker yaml configuration file to use with the command."
                    """)]
    public string? ConfigurationFile { get; init; }


    [AllPathsInEnumerableValid, Option('w', "overwrite-files", Default = null,
         HelpText = """
                    List of files to overwrite the mocker configuration with, The first file overwrites the mocker
                    configuration file and then the one after it overwrite the result and so on...
                    """)]
    public IList<string> OverwriteFiles { get; init; } = [];


    [Option('r', "overwrite-arguments", Default = null,
        HelpText = """
                   List of arguments to overwrite the mocker configuration with, The first argument overwrites the 
                   mocker configuration and then the one after it overwrites the result and so on...
                   For example: `Path:To:Variable:To:Overwrite=NewVariableValue`
                   """)]
    public IList<string> OverwriteArguments { get; init; } = [];


    [Option("no-env", Default = false,
        HelpText = """
                   When this flag is used environment variables will not override loaded configurations.
                   """)]
    public bool DontResolveWithEnvironmentVariables { get; init; } = false;


    [Option('o', "output-folder", Default = null,
         HelpText = """
                    Path to a folder to write the generated templates in.
                    """)]
    public string? TemplatesOutputFolder { get; init; }

    [Option("run-locally", Default = false,
         HelpText = """
                    Runs the project locally and enables exit by any key press.
                    """)]
    public bool RunLocally { get; init; }
}
