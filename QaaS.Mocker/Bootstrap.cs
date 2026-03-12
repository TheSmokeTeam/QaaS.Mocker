using System.Reflection;
using CommandLine;
using QaaS.Mocker.Loaders;
using QaaS.Mocker.Options;

namespace QaaS.Mocker;

/// <summary>
/// Bootstrap class responsible for parsing arguments and creating a configured <see cref="Mocker"/>.
/// </summary>
public static class Bootstrap
{
    /// <summary>
    /// Creates a new <see cref="Mocker"/> from command-line arguments.
    /// </summary>
    public static Mocker New(IEnumerable<string>? args = null)
    {
        if (args == null)
            return new Mocker(null);

        var normalizedArguments = NormalizeArguments(args);
        using var cliParser = CommandLineBuilders.ParserBuilder.BuildParser();
        var cliParserResult = cliParser.ParseArguments<MockerOptions>(normalizedArguments);

        return cliParserResult
            .WithNotParsed(_ => Console.Out.WriteLine(CommandLine.Text.HelpText.AutoBuild(cliParserResult)))
            .MapResult(
                options =>
                {
                    using var loader = new MockerLoader(options);
                    return loader.GetLoadedRunner();
                },
                HandleParseError);
    }

    /// <summary>
    /// Supports verb-style aliases such as <c>run</c>, <c>lint</c>, and <c>template</c> in addition
    /// to the explicit <c>--mode</c> flag.
    /// </summary>
    internal static string[] NormalizeArguments(IEnumerable<string> args)
    {
        var arguments = args.ToArray();
        if (arguments.Length == 0)
            return arguments;

        if (arguments[0].StartsWith('-'))
            return arguments;

        return Enum.TryParse<ExecutionMode>(arguments[0], ignoreCase: true, out _)
            ? ["--mode", arguments[0], .. arguments[1..]]
            : arguments;
    }

    private static Mocker HandleParseError(IEnumerable<Error> errors)
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
        }

        return new Mocker(null);
    }

    private static string GetAssemblyVersionFromName(string assemblyName) =>
        Assembly.Load(assemblyName)
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()!
            .InformationalVersion;
}
