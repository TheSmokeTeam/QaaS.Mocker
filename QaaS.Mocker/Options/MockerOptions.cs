using System.ComponentModel.DataAnnotations;
using CommandLine;
using QaaS.Framework.Configurations.CustomValidationAttributes;
using QaaS.Framework.Executions.Options;

namespace QaaS.Mocker.Options;

/// <summary>
/// Shared command-line options for QaaS.Mocker commands.
/// </summary>
[System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
public abstract record MockerOptions : LoggerOptions
{
    /// <summary>
    /// Gets the main YAML configuration file path.
    /// </summary>
    [Required, ValidPath, Value(0, Default = Constants.DefaultMockerConfigurationFileName,
         HelpText = """
                    Path to a mocker yaml configuration file to use with the command.
                    """)]
    public string? ConfigurationFile { get; init; }


    /// <summary>
    /// Gets the ordered list of YAML overlay files applied after the main configuration.
    /// </summary>
    [AllPathsInEnumerableValid, Option('w', "overwrite-files", Default = null,
         HelpText = """
                    List of files to overwrite the mocker configuration with, The first file overwrites the mocker
                    configuration file and then the one after it overwrite the result and so on...
                    """)]
    public IList<string> OverwriteFiles { get; init; } = [];

    /// <summary>
    /// Gets the ordered list of folders whose YAML files are applied alphabetically after file overlays.
    /// </summary>
    [AllPathsInEnumerableValid, Option('f', "overwrite-folders", Default = null,
         HelpText = """
                    List of folders whose yaml files overwrite the mocker configuration in alphabetical order,
                    after overwrite files and in the order the folders are given.
                    """)]
    public IList<string> OverwriteFolders { get; init; } = [];


    /// <summary>
    /// Gets the ordered list of configuration-path assignments applied after file overlays.
    /// </summary>
    [Option('r', "overwrite-arguments", Default = null,
        HelpText = """
                   List of arguments to overwrite the mocker configuration with, The first argument overwrites the 
                   mocker configuration and then the one after it overwrites the result and so on...
                   For example: `Path:To:Variable:To:Overwrite=NewVariableValue`
                   """)]
    public IList<string> OverwriteArguments { get; init; } = [];


    /// <summary>
    /// Gets whether environment variable overrides should be skipped.
    /// </summary>
    [Option("no-env", Default = false,
        HelpText = """
                   When this flag is used environment variables will not override loaded configurations.
                   """)]
    public bool DontResolveWithEnvironmentVariables { get; init; } = false;


    /// <summary>
    /// Gets the folder used to write generated template output.
    /// </summary>
    [Option('o', "output-folder", Default = null,
         HelpText = """
                    Path to a folder to write the generated templates in.
                    """)]
    public string? TemplatesOutputFolder { get; init; }

    /// <summary>
    /// Gets whether the runtime should stay attached to the local console and stop on key press.
    /// </summary>
    [Option("run-locally", Default = false,
         HelpText = """
                    Runs the project locally and enables exit by any key press.
                    """)]
    public bool RunLocally { get; init; }

    /// <summary>
    /// Gets the execution mode represented by the concrete command.
    /// </summary>
    public abstract ExecutionMode GetExecutionMode();
}
