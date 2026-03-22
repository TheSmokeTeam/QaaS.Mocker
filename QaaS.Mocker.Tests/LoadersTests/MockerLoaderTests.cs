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
        var exception = Assert.Throws<InvalidConfigurationsException>(() => new MockerLoader<RunOptions>(new RunOptions
        {
            ConfigurationFile = null
        }));

        Assert.That(exception, Is.Not.Null);
        Assert.That(exception!.Message, Does.Contain("Given command arguments are not valid"));
    }

    [Test]
    public void GetLoadedContext_AppliesOverwriteFilesAndArguments()
    {
        var tempDirectory = CreateTempDirectory();
        try
        {
            var configFile = WriteFile(tempDirectory, "mocker.qaas.yaml", """
                Server:
                  Http:
                    Port: 8443
                """);
            var overwriteFile = WriteFile(tempDirectory, "overwrite.qaas.yaml", """
                Server:
                  Http:
                    Port: 18080
                """);

            var loader = new MockerLoader<RunOptions>(new RunOptions
            {
                ConfigurationFile = configFile,
                OverwriteFiles = [overwriteFile],
                OverwriteArguments = ["Server:Http:Port=5001"]
            });

            var context = InvokeGetLoadedContext(loader);

            Assert.That(context.RootConfiguration["Server:Http:Port"], Is.EqualTo("5001"));
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
                  Http:
                    Port: 8443
                """);
            var loader = new MockerLoader<TemplateOptions>(new TemplateOptions
            {
                ConfigurationFile = configFile
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
    public void GetLoadedContext_AppliesOverwriteFoldersInAlphabeticalOrder()
    {
        var tempDirectory = CreateTempDirectory();
        try
        {
            var configFile = WriteFile(tempDirectory, "mocker.qaas.yaml", """
                Server:
                  Http:
                    Port: 8443
                """);
            var overwriteFolder = Path.Combine(tempDirectory, "overrides");
            Directory.CreateDirectory(overwriteFolder);
            WriteFile(overwriteFolder, "20-http.yaml", """
                Server:
                  Http:
                    Port: 19090
                """);
            WriteFile(overwriteFolder, "10-http.yaml", """
                Server:
                  Http:
                    Port: 18080
                """);
            WriteFile(overwriteFolder, "readme.txt", "ignored");

            var loader = new MockerLoader<RunOptions>(new RunOptions
            {
                ConfigurationFile = configFile,
                OverwriteFolders = [overwriteFolder]
            });

            var context = InvokeGetLoadedContext(loader);

            Assert.That(context.RootConfiguration["Server:Http:Port"], Is.EqualTo("19090"));
        }
        finally
        {
            DeleteDirectory(tempDirectory);
        }
    }

    [Test]
    public void GetLoadedContext_WithScopedEnvironmentOverride_AppliesEnvironmentVariable()
    {
        const string environmentVariableName = "Server__Http__Port";
        var originalValue = Environment.GetEnvironmentVariable(environmentVariableName);
        var tempDirectory = CreateTempDirectory();
        try
        {
            Environment.SetEnvironmentVariable(environmentVariableName, "5001");
            var configFile = WriteFile(tempDirectory, "mocker.qaas.yaml", """
                Server:
                  Http:
                    Port: 8443
                """);
            var loader = new MockerLoader<RunOptions>(new RunOptions
            {
                ConfigurationFile = configFile
            });

            var context = InvokeGetLoadedContext(loader);

            Assert.That(context.RootConfiguration["Server:Http:Port"], Is.EqualTo("5001"));
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
                  Http:
                    Port: 8443
                """);
            var loader = new MockerLoader<RunOptions>(new RunOptions
            {
                ConfigurationFile = configFile
            });

            var context = InvokeGetLoadedContext(loader);

            Assert.Multiple(() =>
            {
                Assert.That(context.RootConfiguration["JETBRAINS_INTELLIJ_ASK_PSREADLINE_UPDATE"], Is.Null);
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
        const string environmentVariableName = "Servers__0__Http__Port";
        var originalValue = Environment.GetEnvironmentVariable(environmentVariableName);
        var tempDirectory = CreateTempDirectory();
        try
        {
            Environment.SetEnvironmentVariable(environmentVariableName, "5001");
            var configFile = WriteFile(tempDirectory, "mocker.qaas.yaml", """
                Servers:
                  - Http:
                      Port: 8443
                """);
            var loader = new MockerLoader<RunOptions>(new RunOptions
            {
                ConfigurationFile = configFile
            });

            var context = InvokeGetLoadedContext(loader);

            Assert.That(context.RootConfiguration["Servers:0:Http:Port"], Is.EqualTo("5001"));
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
        const string environmentVariableName = "Server__Http__Port";
        var originalValue = Environment.GetEnvironmentVariable(environmentVariableName);
        var tempDirectory = CreateTempDirectory();
        try
        {
            Environment.SetEnvironmentVariable(environmentVariableName, "5001");
            var configFile = WriteFile(tempDirectory, "mocker.qaas.yaml", """
                Server:
                  Http:
                    Port: 8443
                """);
            var loader = new MockerLoader<RunOptions>(new RunOptions
            {
                ConfigurationFile = configFile,
                DontResolveWithEnvironmentVariables = true
            });

            var context = InvokeGetLoadedContext(loader);

            Assert.That(context.RootConfiguration["Server:Http:Port"], Is.EqualTo("8443"));
        }
        finally
        {
            Environment.SetEnvironmentVariable(environmentVariableName, originalValue);
            DeleteDirectory(tempDirectory);
        }
    }

    [TestCase("Server__Http__Port", true, "Server:Http:Port")]
    [TestCase("Server:Http:Port", true, "Server:Http:Port")]
    [TestCase("__", false, "")]
    [TestCase("PATH", false, "")]
    public void TryMapEnvironmentVariableToConfigurationPath_MapsOnlySupportedRoots(
        string variableName,
        bool expectedMapped,
        string expectedPath)
    {
        var tryMapMethod = typeof(MockerLoader<RunOptions>)
            .GetMethod("TryMapEnvironmentVariableToConfigurationPath", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new MissingMethodException(typeof(MockerLoader<RunOptions>).FullName,
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
                  Http:
                    Port: 8443
                """);
            var loader = new MockerLoader<RunOptions>(new RunOptions
            {
                ConfigurationFile = configFile,
                OverwriteFiles = null!,
                OverwriteFolders = null!,
                OverwriteArguments = null!
            });

            var context = InvokeGetLoadedContext(loader);

            Assert.That(context.RootConfiguration["Server:Http:Port"], Is.EqualTo("8443"));
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
                  Http:
                    Port: 8443
                """);
            var loader = new MockerLoader<RunOptions>(new RunOptions
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
                  Http:
                    Port: 8443
                """);
            var contextBuilder = new ContextBuilder(new ConfigurationBuilder());
            contextBuilder.SetLogger(NullLogger.Instance);
            contextBuilder.SetConfigurationFile(configFile);

            MockerLoader<RunOptions>.ApplyEnvironmentOverrides(
                contextBuilder,
                [
                    new KeyValuePair<string?, string?>(null, "ignored"),
                    new KeyValuePair<string?, string?>("", "ignored"),
                    new KeyValuePair<string?, string?>("PATH", "ignored"),
                    new KeyValuePair<string?, string?>("Server__Http__Port", "5001"),
                    new KeyValuePair<string?, string?>("Servers__0__Http__Port", "6001"),
                    new KeyValuePair<string?, string?>("Server__Http__Port", null)
                ],
                NullLogger.Instance);

            var context = contextBuilder.BuildInternal();

            Assert.Multiple(() =>
            {
                Assert.That(context.RootConfiguration["Server:Http:Port"], Is.EqualTo("5001"));
                Assert.That(context.RootConfiguration["Servers:0:Http:Port"], Is.EqualTo("6001"));
            });
        }
        finally
        {
            DeleteDirectory(tempDirectory);
        }
    }

    private static InternalContext InvokeGetLoadedContext<TOptions>(MockerLoader<TOptions> loader)
        where TOptions : MockerOptions
    {
        var loaderType = typeof(MockerLoader<TOptions>);
        var getLoadedContextMethod = loaderType
            .GetMethod("GetLoadedContext", BindingFlags.NonPublic | BindingFlags.Instance)
            ?? throw new MissingMethodException(loaderType.FullName, "GetLoadedContext");

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

