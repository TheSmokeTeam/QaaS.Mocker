using NUnit.Framework;

namespace QaaS.Mocker.Tests;

[TestFixture]
public class CommandLinePathNormalizerTests
{
    [Test]
    public void Normalize_AddsNoEnvFlag_WhenMissing()
    {
        using var sandbox = new TemporaryDirectorySandbox();

        var normalizedArguments = CommandLinePathNormalizer.Normalize(
            ["run", "mocker.qaas.yaml"],
            sandbox.CallerDirectory,
            sandbox.FallbackDirectory,
            includeNoEnvFlag: true);

        Assert.That(normalizedArguments, Does.Contain("--no-env"));
    }

    [Test]
    public void Normalize_RewritesConfigurationFileAgainstCallerDirectory_WhenFileExistsThere()
    {
        using var sandbox = new TemporaryDirectorySandbox();
        var callerConfigPath = sandbox.CreateCallerFile("configs\\custom.yaml");

        var normalizedArguments = CommandLinePathNormalizer.Normalize(
            ["run", "configs\\custom.yaml"],
            sandbox.CallerDirectory,
            sandbox.FallbackDirectory,
            includeNoEnvFlag: true);

        Assert.That(normalizedArguments, Is.EqualTo(new[] { "run", callerConfigPath, "--no-env" }));
    }

    [Test]
    public void Normalize_FallsBackToExampleDirectory_WhenSampleConfigIsNotInCallerDirectory()
    {
        using var sandbox = new TemporaryDirectorySandbox();
        var exampleConfigPath = sandbox.CreateFallbackFile("mocker.qaas.yaml");

        var normalizedArguments = CommandLinePathNormalizer.Normalize(
            ["run", "mocker.qaas.yaml"],
            sandbox.CallerDirectory,
            sandbox.FallbackDirectory,
            includeNoEnvFlag: true);

        Assert.That(normalizedArguments, Is.EqualTo(new[] { "run", exampleConfigPath, "--no-env" }));
    }

    [Test]
    public void Normalize_PreservesRootedConfigurationFilePath()
    {
        using var sandbox = new TemporaryDirectorySandbox();
        var rootedConfigPath = sandbox.CreateCallerFile("configs\\custom.yaml");

        var normalizedArguments = CommandLinePathNormalizer.Normalize(
            ["run", rootedConfigPath],
            sandbox.CallerDirectory,
            sandbox.FallbackDirectory);

        Assert.That(normalizedArguments, Is.EqualTo(new[] { "run", rootedConfigPath }));
    }

    [Test]
    public void Normalize_RewritesOutputFolderAgainstCallerDirectory()
    {
        using var sandbox = new TemporaryDirectorySandbox();
        var exampleConfigPath = sandbox.CreateFallbackFile("mocker.qaas.yaml");
        var expectedOutputPath = Path.GetFullPath("artifacts\\templates", sandbox.CallerDirectory);

        var normalizedArguments = CommandLinePathNormalizer.Normalize(
            ["template", "mocker.qaas.yaml", "--output-folder", "artifacts\\templates"],
            sandbox.CallerDirectory,
            sandbox.FallbackDirectory,
            includeNoEnvFlag: true);

        Assert.That(
            normalizedArguments,
            Is.EqualTo(new[] { "template", exampleConfigPath, "--output-folder", expectedOutputPath, "--no-env" }));
    }

    [Test]
    public void Normalize_PreservesRootedOutputFolderPath()
    {
        using var sandbox = new TemporaryDirectorySandbox();
        var exampleConfigPath = sandbox.CreateFallbackFile("mocker.qaas.yaml");
        var rootedOutputPath = Path.GetFullPath("artifacts\\templates", sandbox.CallerDirectory);

        var normalizedArguments = CommandLinePathNormalizer.Normalize(
            ["template", "mocker.qaas.yaml", "--output-folder", rootedOutputPath],
            sandbox.CallerDirectory,
            sandbox.FallbackDirectory);

        Assert.That(
            normalizedArguments,
            Is.EqualTo(new[] { "template", exampleConfigPath, "--output-folder", rootedOutputPath }));
    }

    [Test]
    public void Normalize_RewritesOverwriteFilesButLeavesOverwriteArgumentsUntouched()
    {
        using var sandbox = new TemporaryDirectorySandbox();
        var exampleConfigPath = sandbox.CreateFallbackFile("mocker.qaas.yaml");
        var overwriteFilePath = sandbox.CreateCallerFile("overrides\\override.yaml");

        var normalizedArguments = CommandLinePathNormalizer.Normalize(
            [
                "run",
                "mocker.qaas.yaml",
                "--overwrite-files",
                "overrides\\override.yaml",
                "--overwrite-arguments",
                "Stubs:0:Name=Overridden"
            ],
            sandbox.CallerDirectory,
            sandbox.FallbackDirectory,
            includeNoEnvFlag: true);

        Assert.That(
            normalizedArguments,
            Is.EqualTo(new[]
            {
                "run",
                exampleConfigPath,
                "--overwrite-files",
                overwriteFilePath,
                "--overwrite-arguments",
                "Stubs:0:Name=Overridden",
                "--no-env"
            }));
    }

    [Test]
    public void Normalize_RewritesOverwriteFolders()
    {
        using var sandbox = new TemporaryDirectorySandbox();
        var exampleConfigPath = sandbox.CreateFallbackFile("mocker.qaas.yaml");
        var overwriteFolderPath = sandbox.CreateCallerDirectory("overrides");

        var normalizedArguments = CommandLinePathNormalizer.Normalize(
            [
                "run",
                "mocker.qaas.yaml",
                "--overwrite-folders",
                "overrides"
            ],
            sandbox.CallerDirectory,
            sandbox.FallbackDirectory,
            includeNoEnvFlag: true);

        Assert.That(
            normalizedArguments,
            Is.EqualTo(new[]
            {
                "run",
                exampleConfigPath,
                "--overwrite-folders",
                overwriteFolderPath,
                "--no-env"
            }));
    }

    [Test]
    public void Normalize_PreservesUnknownLeadingCommand_AndDoesNotDuplicateNoEnv()
    {
        using var sandbox = new TemporaryDirectorySandbox();
        var overwriteFilePath = sandbox.CreateCallerFile("overrides\\override.yaml");
        var overwriteFolderPath = sandbox.CreateCallerDirectory("folders");
        var expectedOutputPath = Path.GetFullPath("artifacts\\templates", sandbox.CallerDirectory);

        var normalizedArguments = CommandLinePathNormalizer.Normalize(
            [
                "serve",
                "configs\\custom.yaml",
                "-w=overrides\\override.yaml",
                "-f=folders",
                "-o=artifacts\\templates",
                "--no-env"
            ],
            sandbox.CallerDirectory,
            sandbox.FallbackDirectory,
            includeNoEnvFlag: true);

        Assert.That(
            normalizedArguments,
            Is.EqualTo(new[]
            {
                "serve",
                sandbox.CallerRelativePath("configs\\custom.yaml"),
                "-w=" + overwriteFilePath,
                "-f=" + overwriteFolderPath,
                "-o=" + expectedOutputPath,
                "--no-env"
            }));
    }

    private sealed class TemporaryDirectorySandbox : IDisposable
    {
        private readonly string _rootDirectory = Path.Combine(
            Path.GetTempPath(),
            "qaas-mocker-example-tests",
            Guid.NewGuid().ToString("N"));

        public TemporaryDirectorySandbox()
        {
            CallerDirectory = Directory.CreateDirectory(Path.Combine(_rootDirectory, "caller")).FullName;
            FallbackDirectory = Directory.CreateDirectory(Path.Combine(_rootDirectory, "fallback")).FullName;
        }

        public string CallerDirectory { get; }

        public string FallbackDirectory { get; }

        public string CreateCallerFile(string relativePath) => CreateFile(CallerDirectory, relativePath);

        public string CreateFallbackFile(string relativePath) => CreateFile(FallbackDirectory, relativePath);

        public string CreateCallerDirectory(string relativePath) => CreateDirectory(CallerDirectory, relativePath);

        public string CallerRelativePath(string relativePath) => Path.GetFullPath(relativePath, CallerDirectory);

        public void Dispose()
        {
            if (Directory.Exists(_rootDirectory))
                Directory.Delete(_rootDirectory, recursive: true);
        }

        private static string CreateFile(string rootDirectory, string relativePath)
        {
            var path = Path.Combine(rootDirectory, relativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, "content");
            return path;
        }

        private static string CreateDirectory(string rootDirectory, string relativePath)
        {
            return Directory.CreateDirectory(Path.Combine(rootDirectory, relativePath)).FullName;
        }
    }
}
