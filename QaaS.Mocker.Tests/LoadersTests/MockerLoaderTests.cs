using System.Reflection;
using NUnit.Framework;
using QaaS.Framework.Configurations.CustomExceptions;
using QaaS.Framework.SDK.ContextObjects;
using QaaS.Mocker.Loaders;
using QaaS.Mocker.Options;

namespace QaaS.Mocker.Tests.LoadersTests;

[TestFixture]
public class MockerLoaderTests
{
    [Test]
    public void GetLoadedRunner_WithMissingConfigurationFile_ThrowsInvalidConfigurationsException()
    {
        var exception = Assert.Throws<InvalidConfigurationsException>(() => new MockerLoader(new MockerOptions
        {
            ConfigurationFile = null
        }));

        Assert.That(exception, Is.Not.Null);
        Assert.That(exception!.Message, Does.Contain("Given command arguments are not valid"));
    }

    [Test]
    public void GetLoadedRunner_WithMissingExecutionMode_ThrowsArgumentException()
    {
        var tempDirectory = CreateTempDirectory();
        try
        {
            var configFile = WriteFile(tempDirectory, "mocker.qaas.yaml", """
                Server:
                  Type: Http
                """);
            var loader = new MockerLoader(new MockerOptions
            {
                ConfigurationFile = configFile,
                ExecutionMode = null
            });

            var exception = Assert.Throws<ArgumentException>(() => loader.GetLoadedRunner());

            Assert.Multiple(() =>
            {
                Assert.That(exception, Is.Not.Null);
                Assert.That(exception!.ParamName, Is.EqualTo("ExecutionMode"));
            });
        }
        finally
        {
            DeleteDirectory(tempDirectory);
        }
    }

    [Test]
    public void GetLoadedContext_AppliesOverwriteFilesAndArguments()
    {
        var tempDirectory = CreateTempDirectory();
        try
        {
            var configFile = WriteFile(tempDirectory, "mocker.qaas.yaml", """
                Server:
                  Type: Http
                """);
            var overwriteFile = WriteFile(tempDirectory, "overwrite.qaas.yaml", """
                Server:
                  Type: Socket
                """);

            var loader = new MockerLoader(new MockerOptions
            {
                ConfigurationFile = configFile,
                OverwriteFiles = [overwriteFile],
                OverwriteArguments = ["Server:Type=Grpc"]
            });

            var context = InvokeGetLoadedContext(loader);

            Assert.That(context.RootConfiguration["Server:Type"], Is.EqualTo("Grpc"));
        }
        finally
        {
            DeleteDirectory(tempDirectory);
        }
    }

    [Test]
    public void GetLoadedRunner_WithValidOptions_ReturnsMocker()
    {
        var tempDirectory = CreateTempDirectory();
        try
        {
            var configFile = WriteFile(tempDirectory, "mocker.qaas.yaml", """
                Server:
                  Type: Http
                """);
            var loader = new MockerLoader(new MockerOptions
            {
                ConfigurationFile = configFile,
                ExecutionMode = ExecutionMode.Lint
            });

            var runner = loader.GetLoadedRunner();

            Assert.That(runner, Is.Not.Null);
            Assert.That(runner, Is.TypeOf<Mocker>());
        }
        finally
        {
            DeleteDirectory(tempDirectory);
        }
    }

    private static InternalContext InvokeGetLoadedContext(MockerLoader loader)
    {
        var getLoadedContextMethod = typeof(MockerLoader)
            .GetMethod("GetLoadedContext", BindingFlags.NonPublic | BindingFlags.Instance)
            ?? throw new MissingMethodException(typeof(MockerLoader).FullName, "GetLoadedContext");

        return (InternalContext)(getLoadedContextMethod.Invoke(loader, null)
                                 ?? throw new InvalidOperationException("Context was not loaded."));
    }

    private static string CreateTempDirectory()
    {
        var tempDirectory = Path.Combine(Path.GetTempPath(), "QaaS.Mocker.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDirectory);
        return tempDirectory;
    }

    private static string WriteFile(string directory, string fileName, string content)
    {
        var path = Path.Combine(directory, fileName);
        File.WriteAllText(path, content);
        return path;
    }

    private static void DeleteDirectory(string directory)
    {
        if (Directory.Exists(directory))
            Directory.Delete(directory, recursive: true);
    }
}
