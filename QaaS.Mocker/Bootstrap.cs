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

        using var cliParser = CommandLineBuilders.ParserBuilder.BuildParser();
        var cliParserResult = cliParser.ParseArguments<MockerOptions>(args);

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
