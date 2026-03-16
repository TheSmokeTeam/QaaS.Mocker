var callerWorkingDirectory = Directory.GetCurrentDirectory();
var exampleWorkingDirectory = AppContext.BaseDirectory;
var normalizedArguments = QaaS.Mocker.CommandLinePathNormalizer.Normalize(
    args,
    callerWorkingDirectory,
    exampleWorkingDirectory,
    includeNoEnvFlag: true);

Directory.SetCurrentDirectory(exampleWorkingDirectory);
QaaS.Mocker.Bootstrap.New(normalizedArguments).Run();
