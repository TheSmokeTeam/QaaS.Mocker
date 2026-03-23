using System.Reflection;
using CommandLine;
using QaaS.Mocker.CommandLineBuilders;
using QaaS.Mocker.Loaders;
using QaaS.Mocker.Options;

namespace QaaS.Mocker;

/// <summary>
/// Bootstrap class responsible for parsing arguments and creating a configured <see cref="MockerRunner"/>.
/// </summary>
public static class Bootstrap
{
    /// <summary>
    /// Creates a new MockerRunner instance from the supplied bootstrap inputs.
    /// </summary>
    /// <remarks>
    /// This is the primary code-first entry point for bootstrapping the product from command-line style arguments so library startup and CLI startup stay aligned.
    /// </remarks>
    /// <qaas-docs group="Getting Started" subgroup="Bootstrap" />
    public static MockerRunner New(IEnumerable<string>? args = null)
    {
        return GetRunner<MockerRunner>(args);
    }

    /// <summary>
    /// Creates a new runner instance from the supplied bootstrap inputs.
    /// </summary>
    /// <remarks>
    /// Use this overload when the mocker run should be represented by a custom <typeparamref name="TRunner" />
    /// implementation while keeping the same command-line bootstrap flow.
    /// </remarks>
    /// <qaas-docs group="Getting Started" subgroup="Bootstrap" />
    public static MockerRunner New<TRunner>(IEnumerable<string>? args = null) where TRunner : MockerRunner
    {
        return GetRunner<TRunner>(args);
    }

    /// <summary>
    /// Creates a new runner instance with custom logic.
    /// </summary>
    /// <param name="args">Command-line arguments.</param>
    /// <returns>A configured <typeparamref name="TRunner" /> instance.</returns>
    internal static TRunner GetRunner<TRunner>(IEnumerable<string>? args)
        where TRunner : MockerRunner
    {
        var normalizedArguments = NormalizeArguments(args ?? []);
        var effectiveTopLevelArguments = RemoveTopLevelIgnorableOptions(normalizedArguments);
        using var cliParser = CommandLineBuilders.ParserBuilder.BuildParser();
        if (effectiveTopLevelArguments.Length == 0)
        {
            var emptyArgsResult = ParseSupportedArguments(cliParser, ["--help"]);
            return WriteHelpAndCreateBootstrapHandledRunner<TRunner>(cliParser, emptyArgsResult,
                includeCommandHelp: true);
        }

        if (IsTopLevelHelpRequest(effectiveTopLevelArguments))
        {
            var topLevelHelpResult = ParseSupportedArguments(cliParser, ["--help"]);
            return WriteHelpAndCreateBootstrapHandledRunner<TRunner>(cliParser, topLevelHelpResult,
                includeCommandHelp: true);
        }

        if (IsCommandHelpRequest(normalizedArguments))
        {
            var commandHelpResult = ParseSupportedArguments(cliParser, normalizedArguments);
            return WriteHelpAndCreateBootstrapHandledRunner<TRunner>(cliParser, commandHelpResult,
                includeCommandHelp: false);
        }

        var cliParserResult = ParseSupportedArguments(cliParser, normalizedArguments);

        return cliParserResult
            .MapResult(
                (RunOptions options) =>
                {
                    using var loader = new MockerLoader<TRunner, RunOptions>(options);
                    return loader.GetLoadedRunner();
                },
                (TemplateOptions options) =>
                {
                    using var loader = new MockerLoader<TRunner, TemplateOptions>(options);
                    return loader.GetLoadedRunner();
                },
                errors => HandleParseError<TRunner>(cliParser, cliParserResult, errors));
    }

    /// <summary>
    /// Preserves the original argument sequence while restoring the legacy
    /// "config path implies run" startup path.
    /// </summary>
    internal static string[] NormalizeArguments(IEnumerable<string> args)
    {
        var arguments = args.ToArray();
        if (!ShouldAssumeRunMode(arguments))
            return arguments;

        return ["run", .. arguments];
    }

    private static TRunner HandleParseError<TRunner>(
        Parser cliParser,
        ParserResult<object> cliParserResult,
        IEnumerable<Error> errors)
        where TRunner : MockerRunner
    {
        var errorsArray = errors.ToArray();

        if (errorsArray.All(err => err.Tag is ErrorType.VersionRequestedError))
        {
            const string qaasFrameworkAssemblyName = "QaaS.Framework.Executions";
            const string qaasMockerAssemblyName = "QaaS.Mocker";
            Console.Out.WriteLine(
                $"\nQaaS Framework Versions:\n" +
                $"{qaasFrameworkAssemblyName} {GetAssemblyVersionFromName(qaasFrameworkAssemblyName)}\n" +
                $"{qaasMockerAssemblyName} {GetAssemblyVersionFromName(qaasMockerAssemblyName)}\n");
            return CreateBootstrapHandledRunner<TRunner>(0);
        }

        WriteHelpText(cliParser, cliParserResult, includeCommandHelp: false);
        return CreateBootstrapHandledRunner<TRunner>(1);
    }

    private static string GetAssemblyVersionFromName(string assemblyName) =>
        Assembly.Load(assemblyName)
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()!
            .InformationalVersion;

    private static ParserResult<object> ParseSupportedArguments(Parser cliParser, IEnumerable<string> args)
    {
        return cliParser.ParseArguments<RunOptions, TemplateOptions>(args);
    }

    private static void WriteHelpText(
        Parser cliParser,
        ParserResult<object> cliParserResult,
        bool includeCommandHelp)
    {
        Console.Out.WriteLine(HelpTextBuilder.BuildHelpText(cliParser, cliParserResult, includeCommandHelp));
    }

    internal static TRunner CreateRunner<TRunner>(ExecutionBuilder? executionBuilder, Action<int>? exitAction = null)
        where TRunner : MockerRunner
    {
        return (TRunner)Activator.CreateInstance(typeof(TRunner), executionBuilder, exitAction)!;
    }

    private static TRunner WriteHelpAndCreateBootstrapHandledRunner<TRunner>(
        Parser cliParser,
        ParserResult<object> cliParserResult,
        bool includeCommandHelp,
        int exitCode = 0)
        where TRunner : MockerRunner
    {
        WriteHelpText(cliParser, cliParserResult, includeCommandHelp);
        return CreateBootstrapHandledRunner<TRunner>(exitCode);
    }

    private static TRunner CreateBootstrapHandledRunner<TRunner>(int exitCode)
        where TRunner : MockerRunner
    {
        return (TRunner)CreateRunner<TRunner>(null).WithBootstrapHandledExitCode(exitCode);
    }

    private static bool IsExecutionModeAlias(string argument)
    {
        return Enum.TryParse<ExecutionMode>(argument, ignoreCase: true, out _);
    }

    private static bool IsTopLevelHelpRequest(IReadOnlyList<string> arguments)
    {
        return arguments.Count == 1 && IsHelpOption(arguments[0]);
    }

    private static bool IsCommandHelpRequest(IReadOnlyList<string> arguments)
    {
        return arguments.Count >= 2 &&
               IsExecutionModeAlias(arguments[0]) &&
               arguments.Skip(1).Any(IsHelpOption);
    }

    private static bool IsHelpOption(string argument)
    {
        return string.Equals(argument, "--help", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(argument, "-h", StringComparison.OrdinalIgnoreCase);
    }

    private static bool ShouldAssumeRunMode(IReadOnlyList<string> arguments)
    {
        var effectiveArguments = RemoveTopLevelIgnorableOptions(arguments);
        if (effectiveArguments.Length == 0)
            return false;

        var firstEffectiveArgument = effectiveArguments[0];
        if (IsExecutionModeAlias(firstEffectiveArgument) ||
            IsHelpOption(firstEffectiveArgument) ||
            IsOption(firstEffectiveArgument))
        {
            return false;
        }

        return LooksLikeConfigurationPath(firstEffectiveArgument);
    }

    private static bool LooksLikeConfigurationPath(string argument)
    {
        if (Path.IsPathRooted(argument))
            return true;

        if (argument.IndexOfAny(['\\', '/']) >= 0)
            return true;

        var extension = Path.GetExtension(argument);
        return extension.Equals(".yaml", StringComparison.OrdinalIgnoreCase) ||
               extension.Equals(".yml", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsOption(string argument)
    {
        return argument.StartsWith("-", StringComparison.Ordinal);
    }

    private static string[] RemoveTopLevelIgnorableOptions(IEnumerable<string> arguments)
    {
        return arguments
            .Where(argument => !string.Equals(argument, "--no-env", StringComparison.OrdinalIgnoreCase))
            .ToArray();
    }
}
