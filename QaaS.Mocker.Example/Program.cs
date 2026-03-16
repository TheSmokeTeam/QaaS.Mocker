Directory.SetCurrentDirectory(AppContext.BaseDirectory);
QaaS.Mocker.Bootstrap.New(NormalizeExampleArgs(args)).Run();

static string[] NormalizeExampleArgs(IEnumerable<string> args)
{
    var normalizedArguments = args.ToList();
    if (normalizedArguments.All(argument =>
            !string.Equals(argument, "--no-env", StringComparison.OrdinalIgnoreCase)))
    {
        // Keep the sample deterministic in IDE terminals that inject many unrelated environment variables.
        normalizedArguments.Add("--no-env");
    }

    return [.. normalizedArguments];
}
