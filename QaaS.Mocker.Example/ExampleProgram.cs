namespace QaaS.Mocker.Example;

internal static class ExampleProgram
{
    public static void Run(string[] args)
    {
        var originalWorkingDirectory = Directory.GetCurrentDirectory();
        var exampleWorkingDirectory = AppContext.BaseDirectory;
        var normalizedArguments = ExampleArgumentNormalizer.Normalize(
            args,
            originalWorkingDirectory,
            exampleWorkingDirectory);

        Directory.SetCurrentDirectory(exampleWorkingDirectory);
        QaaS.Mocker.Bootstrap.New(normalizedArguments).Run();
    }
}
