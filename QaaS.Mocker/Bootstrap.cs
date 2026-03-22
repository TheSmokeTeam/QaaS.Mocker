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
    /// Creates a new <see cref="MockerRunner"/> from command-line arguments.
    /// </summary>
    public static MockerRunner New(IEnumerable<string>? args = null)
    {
        var normalizedArguments = NormalizeArguments(args ?? []);
        using var cliParser = CommandLineBuilders.ParserBuilder.BuildParser();
        if (normalizedArguments.Length == 0)
        {
            var emptyArgsResult = ParseSupportedArguments(cliParser, ["--help"]);
            return WriteHelpAndCreateBootstrapHandledRunner(cliParser, emptyArgsResult, includeCommandHelp: true);
        }

        if (IsTopLevelHelpRequest(normalizedArguments))
        {
            var topLevelHelpResult = ParseSupportedArguments(cliParser, normalizedArguments);
            return WriteHelpAndCreateBootstrapHandledRunner(cliParser, topLevelHelpResult, includeCommandHelp: true);
        }

        if (IsCommandHelpRequest(normalizedArguments))
        {
            var commandHelpResult = ParseSupportedArguments(cliParser, normalizedArguments);
            return WriteHelpAndCreateBootstrapHandledRunner(cliParser, commandHelpResult, includeCommandHelp: false);
        }

        var cliParserResult = ParseSupportedArguments(cliParser, normalizedArguments);

        return cliParserResult
            .MapResult(
                (RunOptions options) =>
                {
                    using var loader = new MockerLoader<RunOptions>(options);
                    return loader.GetLoadedRunner();
                },
                (LintOptions options) =>
                {
                    using var loader = new MockerLoader<LintOptions>(options);
                    return loader.GetLoadedRunner();
                },
                (TemplateOptions options) =>
                {
                    using var loader = new MockerLoader<TemplateOptions>(options);
                    return loader.GetLoadedRunner();
                },
                errors => HandleParseError(cliParser, cliParserResult, errors));
    }

    /// <summary>
    /// Supports command-style execution and preserves the older implicit-run and <c>--mode</c> forms
    /// so existing hosts and scripts do not break during the CLI transition.
    /// </summary>
    internal static string[] NormalizeArguments(IEnumerable<string> args)
    {
        var arguments = args.ToArray();
        if (arguments.Length == 0)
            return arguments;

        if (TryNormalizeLegacyModeArguments(arguments, out var normalizedLegacyModeArguments))
            return normalizedLegacyModeArguments;

        if (arguments[0].StartsWith('-'))
            return arguments;

        return IsExecutionModeAlias(arguments[0]) ? arguments : ["run", .. arguments];
    }

    private static MockerRunner HandleParseError(
        Parser cliParser,
        ParserResult<object> cliParserResult,
        IEnumerable<Error> errors)
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
            return CreateBootstrapHandledRunner(0);
        }

        WriteHelpText(cliParser, cliParserResult, includeCommandHelp: false);
        return CreateBootstrapHandledRunner(1);
    }

    private static string GetAssemblyVersionFromName(string assemblyName) =>
        Assembly.Load(assemblyName)
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()!
            .InformationalVersion;

    private static ParserResult<object> ParseSupportedArguments(Parser cliParser, IEnumerable<string> args)
    {
        return cliParser.ParseArguments<RunOptions, LintOptions, TemplateOptions>(args);
    }

    private static void WriteHelpText(
        Parser cliParser,
        ParserResult<object> cliParserResult,
        bool includeCommandHelp)
    {
        Console.Out.WriteLine(HelpTextBuilder.BuildHelpText(cliParser, cliParserResult, includeCommandHelp));
    }

    private static MockerRunner WriteHelpAndCreateBootstrapHandledRunner(
        Parser cliParser,
        ParserResult<object> cliParserResult,
        bool includeCommandHelp,
        int exitCode = 0)
    {
        WriteHelpText(cliParser, cliParserResult, includeCommandHelp);
        return CreateBootstrapHandledRunner(exitCode);
    }

    private static MockerRunner CreateBootstrapHandledRunner(int exitCode)
    {
        return new MockerRunner(null).WithBootstrapHandledExitCode(exitCode);
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

    private static bool TryNormalizeLegacyModeArguments(
        IReadOnlyList<string> arguments,
        out string[] normalizedArguments)
    {
        for (var index = 0; index < arguments.Count; index++)
        {
            if (!TryReadModeOption(arguments, index, out var modeValue, out var consumedArgumentCount))
                continue;

            if (!IsExecutionModeAlias(modeValue))
            {
                normalizedArguments = arguments.ToArray();
                return true;
            }

            var rewrittenArguments = new List<string>(arguments.Count);
            rewrittenArguments.Add(modeValue);

            for (var currentIndex = 0; currentIndex < arguments.Count; currentIndex++)
            {
                if (currentIndex == index)
                {
                    currentIndex += consumedArgumentCount - 1;
                    continue;
                }

                rewrittenArguments.Add(arguments[currentIndex]);
            }

            normalizedArguments = rewrittenArguments.ToArray();
            return true;
        }

        normalizedArguments = [];
        return false;
    }

    private static bool TryReadModeOption(
        IReadOnlyList<string> arguments,
        int index,
        out string modeValue,
        out int consumedArgumentCount)
    {
        var argument = arguments[index];
        if (string.Equals(argument, "--mode", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(argument, "-m", StringComparison.OrdinalIgnoreCase))
        {
            if (index + 1 < arguments.Count)
            {
                modeValue = arguments[index + 1];
                consumedArgumentCount = 2;
                return true;
            }

            modeValue = string.Empty;
            consumedArgumentCount = 1;
            return false;
        }

        var splitIndex = argument.IndexOf('=');
        if (splitIndex <= 0)
        {
            modeValue = string.Empty;
            consumedArgumentCount = 0;
            return false;
        }

        var optionName = argument[..splitIndex];
        if (!string.Equals(optionName, "--mode", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(optionName, "-m", StringComparison.OrdinalIgnoreCase))
        {
            modeValue = string.Empty;
            consumedArgumentCount = 0;
            return false;
        }

        modeValue = argument[(splitIndex + 1)..];
        consumedArgumentCount = 1;
        return true;
    }
}
