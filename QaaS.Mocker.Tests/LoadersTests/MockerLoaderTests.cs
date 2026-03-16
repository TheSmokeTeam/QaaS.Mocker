using System.Reflection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
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
    public void GetLoadedRunner_WithValidOptions_ReturnsMockerRunner()
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
            Assert.That(runner, Is.TypeOf<MockerRunner>());
        }
        finally
        {
            DeleteDirectory(tempDirectory);
        }
    }

    [Test]
    public void GetLoadedContext_WithScopedEnvironmentOverride_AppliesEnvironmentVariable()
    {
        const string environmentVariableName = "Server__Type";
        var originalValue = Environment.GetEnvironmentVariable(environmentVariableName);
        var tempDirectory = CreateTempDirectory();
        try
        {
            Environment.SetEnvironmentVariable(environmentVariableName, "Grpc");
            var configFile = WriteFile(tempDirectory, "mocker.qaas.yaml", """
                Server:
                  Type: Http
                """);
            var loader = new MockerLoader(new MockerOptions
            {
                ConfigurationFile = configFile
            });

            var context = InvokeGetLoadedContext(loader);

            Assert.That(context.RootConfiguration["Server:Type"], Is.EqualTo("Grpc"));
        }
        finally
        {
            Environment.SetEnvironmentVariable(environmentVariableName, originalValue);
            DeleteDirectory(tempDirectory);
        }
    }

    [Test]
    public void GetLoadedContext_WithUnrelatedEnvironmentVariable_IgnoresIt()
    {
        const string environmentVariableName = "JETBRAINS_INTELLIJ_ASK_PSREADLINE_UPDATE";
        var originalValue = Environment.GetEnvironmentVariable(environmentVariableName);
        var tempDirectory = CreateTempDirectory();
        try
        {
            Environment.SetEnvironmentVariable(environmentVariableName, "1");
            var configFile = WriteFile(tempDirectory, "mocker.qaas.yaml", """
                Server:
                  Type: Http
                  Http:
                    Port: 8443
                """);
            var loader = new MockerLoader(new MockerOptions
            {
                ConfigurationFile = configFile
            });

            var context = InvokeGetLoadedContext(loader);

            Assert.Multiple(() =>
            {
                Assert.That(context.RootConfiguration["JETBRAINS_INTELLIJ_ASK_PSREADLINE_UPDATE"], Is.Null);
                Assert.That(context.RootConfiguration["Server:Type"], Is.EqualTo("Http"));
                Assert.That(context.RootConfiguration["Server:Http:Port"], Is.EqualTo("8443"));
            });
        }
        finally
        {
            Environment.SetEnvironmentVariable(environmentVariableName, originalValue);
            DeleteDirectory(tempDirectory);
        }
    }

    [Test]
    public void GetLoadedContext_WithMultiServerEnvironmentOverride_AppliesEnvironmentVariable()
    {
        const string environmentVariableName = "Servers__0__Type";
        var originalValue = Environment.GetEnvironmentVariable(environmentVariableName);
        var tempDirectory = CreateTempDirectory();
        try
        {
            Environment.SetEnvironmentVariable(environmentVariableName, "Grpc");
            var configFile = WriteFile(tempDirectory, "mocker.qaas.yaml", """
                Servers:
                  - Type: Http
                    Http:
                      Port: 8443
                """);
            var loader = new MockerLoader(new MockerOptions
            {
                ConfigurationFile = configFile
            });

            var context = InvokeGetLoadedContext(loader);

            Assert.That(context.RootConfiguration["Servers:0:Type"], Is.EqualTo("Grpc"));
        }
        finally
        {
            Environment.SetEnvironmentVariable(environmentVariableName, originalValue);
            DeleteDirectory(tempDirectory);
        }
    }

    [Test]
    public void GetLoadedContext_WithDontResolveWithEnvironmentVariables_DoesNotApplyEnvironmentVariable()
    {
        const string environmentVariableName = "Server__Type";
        var originalValue = Environment.GetEnvironmentVariable(environmentVariableName);
        var tempDirectory = CreateTempDirectory();
        try
        {
            Environment.SetEnvironmentVariable(environmentVariableName, "Grpc");
            var configFile = WriteFile(tempDirectory, "mocker.qaas.yaml", """
                Server:
                  Type: Http
                """);
            var loader = new MockerLoader(new MockerOptions
            {
                ConfigurationFile = configFile,
                DontResolveWithEnvironmentVariables = true
            });

            var context = InvokeGetLoadedContext(loader);

            Assert.That(context.RootConfiguration["Server:Type"], Is.EqualTo("Http"));
        }
        finally
        {
            Environment.SetEnvironmentVariable(environmentVariableName, originalValue);
            DeleteDirectory(tempDirectory);
        }
    }

    [TestCase("Server__Http__Port", true, "Server:Http:Port")]
    [TestCase("Server:Type", true, "Server:Type")]
    [TestCase("__", false, "")]
    [TestCase("PATH", false, "")]
    public void TryMapEnvironmentVariableToConfigurationPath_MapsOnlySupportedRoots(
        string variableName,
        bool expectedMapped,
        string expectedPath)
    {
        var tryMapMethod = typeof(MockerLoader)
            .GetMethod("TryMapEnvironmentVariableToConfigurationPath", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new MissingMethodException(typeof(MockerLoader).FullName,
                "TryMapEnvironmentVariableToConfigurationPath");
        var arguments = new object?[] { variableName, null };

        var mapped = (bool)tryMapMethod.Invoke(null, arguments)!;

        Assert.Multiple(() =>
        {
            Assert.That(mapped, Is.EqualTo(expectedMapped));
            Assert.That(arguments[1], Is.EqualTo(expectedPath));
        });
    }

    [Test]
    public void GetLoadedContext_WithNullOverwriteCollections_LoadsContext()
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
                OverwriteFiles = null!,
                OverwriteArguments = null!
            });

            var context = InvokeGetLoadedContext(loader);

            Assert.That(context.RootConfiguration["Server:Type"], Is.EqualTo("Http"));
        }
        finally
        {
            DeleteDirectory(tempDirectory);
        }
    }

    [Test]
    public void Dispose_ReleasesLoaderScopeWithoutThrowing()
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
                ConfigurationFile = configFile
            });

            Assert.DoesNotThrow(() => loader.Dispose());
        }
        finally
        {
            DeleteDirectory(tempDirectory);
        }
    }

    [Test]
    public void ApplyEnvironmentOverrides_IgnoresInvalidEntriesAndAppliesSupportedMappings()
    {
        var tempDirectory = CreateTempDirectory();
        try
        {
            var configFile = WriteFile(tempDirectory, "mocker.qaas.yaml", """
                Server:
                  Type: Http
                  Http:
                    Port: 8443
                """);
            var contextBuilder = new ContextBuilder(new ConfigurationBuilder());
            contextBuilder.SetLogger(NullLogger.Instance);
            contextBuilder.SetConfigurationFile(configFile);

            MockerLoader.ApplyEnvironmentOverrides(
                contextBuilder,
                [
                    new KeyValuePair<string?, string?>(null, "ignored"),
                    new KeyValuePair<string?, string?>("", "ignored"),
                    new KeyValuePair<string?, string?>("PATH", "ignored"),
                    new KeyValuePair<string?, string?>("Server__Type", "Grpc"),
                    new KeyValuePair<string?, string?>("Servers__0__Type", "Socket"),
                    new KeyValuePair<string?, string?>("Server__Http__Port", null)
                ],
                NullLogger.Instance);

            var context = contextBuilder.BuildInternal();

            Assert.Multiple(() =>
            {
                Assert.That(context.RootConfiguration["Server:Type"], Is.EqualTo("Grpc"));
                Assert.That(context.RootConfiguration["Servers:0:Type"], Is.EqualTo("Socket"));
                Assert.That(context.RootConfiguration["Server:Http:Port"], Is.EqualTo("8443"));
            });
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
