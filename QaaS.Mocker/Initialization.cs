using System.Reflection;
using CommandLine;
using QaaS.Mocker.CommandLineBuilders;
using QaaS.Mocker.Executions;

namespace QaaS.Mocker;

/// <summary>
/// Responsible for the initialization of the Mocker Server.
/// </summary>
public static class Initialization
{
    /// <summary>
    /// Initializes and runs the mocker with the passed arguments.
    /// </summary>
    public static void Initialize(IEnumerable<string> args)
    {
        using var cliParser = ParserBuilder.BuildParser();
        var cliParserResult = cliParser.ParseArguments<Options.MockerOptions>(args);
        var exitCode = cliParserResult
            .MapResult(options => new ExecutionFactory(options).Build().Run(), HandleParseError);
        Environment.Exit(exitCode);
    }
    
    private static int HandleParseError(IEnumerable<Error> errors)
    {
        var errorsArray = errors.ToArray();
        
        // If all errors are version requests handle the version request case
        if (errorsArray.All(err => err.Tag is ErrorType.VersionRequestedError))
        {
            const string qaasSdkAssemblyName = "QaaS.SDK", qaasMockerAssemblyName = "QaaS.Mocker";
        
            Console.Out.Write($"\nQaaS Framework Versions:\n" +
                              $"{qaasSdkAssemblyName} {GetAssemblyVersionFromName(qaasSdkAssemblyName)}\n"+
                              $"{qaasMockerAssemblyName} {GetAssemblyVersionFromName(qaasMockerAssemblyName)}\n");
            return 0;
        }
        // If all errors are help commands requested then the user asked for help and arguments are valid
        if (errorsArray.All(err => err.Tag is ErrorType.HelpRequestedError or ErrorType.HelpVerbRequestedError))
            return 0;
        Console.Out.WriteLine("Failed to parse/process the command line arguments");
        return -1;
    }

    private static string GetAssemblyVersionFromName(string assemblyName) => 
        Assembly.Load(assemblyName).GetCustomAttribute<AssemblyInformationalVersionAttribute>()!.InformationalVersion;
}