using QaaS.Mocker.Options;

namespace QaaS.Mocker.Example;

internal static class ExampleArgumentNormalizer
{
    public static string[] Normalize(
        IEnumerable<string> args,
        string callerWorkingDirectory,
        string exampleWorkingDirectory)
    {
        ArgumentNullException.ThrowIfNull(args);
        ArgumentException.ThrowIfNullOrWhiteSpace(callerWorkingDirectory);
        ArgumentException.ThrowIfNullOrWhiteSpace(exampleWorkingDirectory);

        var normalizedArguments = RewriteRelativePaths(
            args.ToArray(),
            callerWorkingDirectory,
            exampleWorkingDirectory);

        if (normalizedArguments.All(argument =>
                !string.Equals(argument, "--no-env", StringComparison.OrdinalIgnoreCase)))
        {
            // Keep the sample deterministic in IDE terminals that inject many unrelated environment variables.
            normalizedArguments.Add("--no-env");
        }

        return [.. normalizedArguments];
    }

    private static List<string> RewriteRelativePaths(
        IReadOnlyList<string> args,
        string callerWorkingDirectory,
        string exampleWorkingDirectory)
    {
        var rewrittenArguments = new List<string>(args.Count + 1);
        var configurationFileResolved = false;

        for (var index = 0; index < args.Count; index++)
        {
            var argument = args[index];

            if (index == 0 && IsExecutionModeAlias(argument))
            {
                rewrittenArguments.Add(argument);
                continue;
            }

            if (TryRewriteSingleValueOption(
                    args,
                    ref index,
                    rewrittenArguments,
                    "--mode",
                    "-m",
                    static value => value))
            {
                continue;
            }

            if (TryRewriteSingleValueOption(
                    args,
                    ref index,
                    rewrittenArguments,
                    "--output-folder",
                    "-o",
                    value => ResolveOutputPath(value, callerWorkingDirectory)))
            {
                continue;
            }

            if (TryRewriteListOption(
                    args,
                    ref index,
                    rewrittenArguments,
                    "--overwrite-files",
                    "-w",
                    value => ResolveInputPath(value, callerWorkingDirectory, exampleWorkingDirectory)))
            {
                continue;
            }

            if (TryRewriteListOption(
                    args,
                    ref index,
                    rewrittenArguments,
                    "--overwrite-arguments",
                    "-r",
                    static value => value))
            {
                continue;
            }

            if (!configurationFileResolved && !IsOption(argument))
            {
                rewrittenArguments.Add(
                    ResolveInputPath(argument, callerWorkingDirectory, exampleWorkingDirectory));
                configurationFileResolved = true;
                continue;
            }

            rewrittenArguments.Add(argument);
        }

        return rewrittenArguments;
    }

    private static bool TryRewriteSingleValueOption(
        IReadOnlyList<string> args,
        ref int index,
        ICollection<string> rewrittenArguments,
        string longName,
        string shortName,
        Func<string, string> transform)
    {
        var argument = args[index];
        if (IsOptionName(argument, longName, shortName))
        {
            rewrittenArguments.Add(argument);
            if (index + 1 < args.Count)
            {
                rewrittenArguments.Add(transform(args[index + 1]));
                index++;
            }

            return true;
        }

        if (!TrySplitOption(argument, longName, shortName, out var optionName, out var value))
            return false;

        rewrittenArguments.Add($"{optionName}={transform(value)}");
        return true;
    }

    private static bool TryRewriteListOption(
        IReadOnlyList<string> args,
        ref int index,
        ICollection<string> rewrittenArguments,
        string longName,
        string shortName,
        Func<string, string> transform)
    {
        var argument = args[index];
        if (IsOptionName(argument, longName, shortName))
        {
            rewrittenArguments.Add(argument);
            while (index + 1 < args.Count && !IsOption(args[index + 1]))
            {
                rewrittenArguments.Add(transform(args[index + 1]));
                index++;
            }

            return true;
        }

        if (!TrySplitOption(argument, longName, shortName, out var optionName, out var value))
            return false;

        rewrittenArguments.Add($"{optionName}={transform(value)}");
        return true;
    }

    private static bool TrySplitOption(
        string argument,
        string longName,
        string shortName,
        out string optionName,
        out string value)
    {
        var splitIndex = argument.IndexOf('=');
        if (splitIndex <= 0)
        {
            optionName = string.Empty;
            value = string.Empty;
            return false;
        }

        optionName = argument[..splitIndex];
        value = argument[(splitIndex + 1)..];
        return IsOptionName(optionName, longName, shortName);
    }

    private static bool IsOptionName(string argument, string longName, string shortName) =>
        string.Equals(argument, longName, StringComparison.OrdinalIgnoreCase) ||
        string.Equals(argument, shortName, StringComparison.OrdinalIgnoreCase);

    private static bool IsOption(string argument) => argument.StartsWith("-", StringComparison.Ordinal);

    private static bool IsExecutionModeAlias(string argument) =>
        !IsOption(argument) && Enum.TryParse<ExecutionMode>(argument, ignoreCase: true, out _);

    private static string ResolveInputPath(
        string path,
        string callerWorkingDirectory,
        string exampleWorkingDirectory)
    {
        if (Path.IsPathRooted(path))
            return path;

        var callerRelativePath = Path.GetFullPath(path, callerWorkingDirectory);
        if (File.Exists(callerRelativePath))
            return callerRelativePath;

        var exampleRelativePath = Path.GetFullPath(path, exampleWorkingDirectory);
        return File.Exists(exampleRelativePath) ? exampleRelativePath : callerRelativePath;
    }

    private static string ResolveOutputPath(string path, string callerWorkingDirectory) =>
        Path.IsPathRooted(path) ? path : Path.GetFullPath(path, callerWorkingDirectory);
}
