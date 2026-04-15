using CommandLine;
using NUnit.Framework;
using QaaS.Framework.Executions;
using QaaS.Mocker.CommandLineBuilders;
using QaaS.Mocker.Options;
using System.IO;

namespace QaaS.Mocker.Tests;

[TestFixture]
public class BootstrapTests
{
    [Test]
    public void New_WithNullArgs_WritesTopLevelHelpAndReturnsBootstrapHandledRunner()
    {
        var output = WithDefaultConfigurationFileInAppBaseDirectory(null, () => CaptureConsoleOut(() =>
        {
            var mocker = Bootstrap.New(null);
            Assert.DoesNotThrow(() => mocker.Run());
        }));

        Assert.Multiple(() =>
        {
            Assert.That(output, Does.Contain("Usage:"));
            Assert.That(output, Does.Contain("Command Details:"));
            Assert.That(output, Does.Contain("run:"));
            Assert.That(output, Does.Contain("template:"));
            Assert.That(output, Does.Contain("Empty arguments only work for code-only hosts"));
        });
    }

    [Test]
    public void New_WithInvalidArgs_ReturnsNoOpMocker()
    {
        var output = CaptureConsoleOut(() =>
        {
            var mocker = Bootstrap.New(["--unknown-option"]);
            Assert.DoesNotThrow(() => mocker.Run());
        });

        Assert.That(output, Does.Contain("run"));
    }

    [Test]
    public void New_WithVersionOption_ReturnsNoOpMocker()
    {
        var output = CaptureConsoleOut(() =>
        {
            var mocker = Bootstrap.New(["--version"]);
            Assert.DoesNotThrow(() => mocker.Run());
        });

        Assert.That(output, Does.Contain("QaaS Framework Versions:"));
    }

    [Test]
    public void New_WithTopLevelHelp_WritesHelpForEachCommand()
    {
        var output = CaptureConsoleOut(() =>
        {
            var mocker = Bootstrap.New(["--help"]);
            Assert.DoesNotThrow(() => mocker.Run());
        });

        Assert.Multiple(() =>
        {
            Assert.That(output, Does.Contain("Command Details:"));
            Assert.That(output, Does.Contain("run:"));
            Assert.That(output, Does.Contain("template:"));
        });
    }

    [Test]
    public void New_WithOnlyNoEnvFlag_WritesHelpForEachCommand()
    {
        var output = CaptureConsoleOut(() =>
        {
            var mocker = Bootstrap.New(["--no-env"]);
            Assert.DoesNotThrow(() => mocker.Run());
        });

        Assert.Multiple(() =>
        {
            Assert.That(output, Does.Contain("Command Details:"));
            Assert.That(output, Does.Contain("run:"));
            Assert.That(output, Does.Contain("template:"));
        });
    }

    [Test]
    public void New_WithTopLevelHelpAndNoEnv_WritesHelpForEachCommand()
    {
        var output = CaptureConsoleOut(() =>
        {
            var mocker = Bootstrap.New(["--help", "--no-env"]);
            Assert.DoesNotThrow(() => mocker.Run());
        });

        Assert.Multiple(() =>
        {
            Assert.That(output, Does.Contain("Command Details:"));
            Assert.That(output, Does.Contain("run:"));
            Assert.That(output, Does.Contain("template:"));
        });
    }

    [Test]
    public void New_WithCommandHelp_WritesRequestedCommandHelpOnly()
    {
        var output = CaptureConsoleOut(() =>
        {
            var mocker = Bootstrap.New(["run", "--help"]);
            Assert.DoesNotThrow(() => mocker.Run());
        });

        Assert.Multiple(() =>
        {
            Assert.That(output, Does.Contain("Path to a mocker yaml configuration file to use with"));
            Assert.That(output, Does.Not.Contain("Command Details:"));
            Assert.That(output, Does.Not.Contain("template:"));
        });
    }

    [Test]
    public void New_WithCommandHelpAndNoEnv_WritesRequestedCommandHelpOnly()
    {
        var output = CaptureConsoleOut(() =>
        {
            var mocker = Bootstrap.New(["run", "--help", "--no-env"]);
            Assert.DoesNotThrow(() => mocker.Run());
        });

        Assert.Multiple(() =>
        {
            Assert.That(output, Does.Contain("Path to a mocker yaml configuration file to use with"));
            Assert.That(output, Does.Not.Contain("Command Details:"));
            Assert.That(output, Does.Not.Contain("template:"));
        });
    }

    [Test]
    public void ParserBuilder_ParsesExecutionModeCaseInsensitive()
    {
        using var parser = ParserBuilder.BuildParser();
        var parserResult = parser.ParseArguments<RunOptions, TemplateOptions>(["tEmPlAtE", "mocker.qaas.yaml"]);

        var parsedExecutionMode = parserResult.MapResult(
            (RunOptions _) => ExecutionMode.Run,
            (TemplateOptions _) => ExecutionMode.Template,
            _ => (ExecutionMode?)null);

        Assert.That(parsedExecutionMode, Is.EqualTo(ExecutionMode.Template));
    }

    [TestCase("run", ExecutionMode.Run)]
    [TestCase("template", ExecutionMode.Template)]
    public void NormalizeArguments_WithVerbAlias_PrependsModeFlag(string verb, ExecutionMode expectedMode)
    {
        var normalizedArguments = Bootstrap.NormalizeArguments([verb, "mocker.qaas.yaml"]);

        Assert.That(normalizedArguments, Is.EqualTo(new[] { verb, "mocker.qaas.yaml" }));

        using var parser = ParserBuilder.BuildParser();
        var parsedExecutionMode = parser.ParseArguments<RunOptions, TemplateOptions>(normalizedArguments)
            .MapResult(
                (RunOptions _) => ExecutionMode.Run,
                (TemplateOptions _) => ExecutionMode.Template,
                _ => (ExecutionMode?)null);

        Assert.Multiple(() =>
        {
            Assert.That(parsedExecutionMode, Is.EqualTo(expectedMode));
            Assert.That(normalizedArguments[1], Is.EqualTo("mocker.qaas.yaml"));
        });
    }

    [Test]
    public void NormalizeArguments_WithOptionFirst_LeavesArgumentsUntouched()
    {
        var normalizedArguments = Bootstrap.NormalizeArguments(["--mode", "run", "mocker.qaas.yaml"]);

        Assert.That(normalizedArguments, Is.EqualTo(new[] { "--mode", "run", "mocker.qaas.yaml" }));
    }

    [Test]
    public void NormalizeArguments_WithNoArguments_ReturnsEmptyArray()
    {
        var normalizedArguments = Bootstrap.NormalizeArguments(
            [],
            @"C:\missing",
            _ => false,
            () => false);

        Assert.That(normalizedArguments, Is.Empty);
    }

    [Test]
    public void NormalizeArguments_WithNoArgumentsAndDefaultConfigurationFileAvailable_UsesRunModeWithAbsolutePath()
    {
        var normalizedArguments = Bootstrap.NormalizeArguments(
            [],
            @"C:\temp",
            _ => true,
            () => false);

        Assert.That(normalizedArguments, Is.Empty);
    }

    [Test]
    public void NormalizeArguments_WithNoArgumentsAndCodeConfigurationAvailable_ReturnsEmptyArray()
    {
        var normalizedArguments = Bootstrap.NormalizeArguments(
            [],
            @"C:\temp",
            _ => false,
            () => true);

        Assert.That(normalizedArguments, Is.Empty);
    }

    [Test]
    public void NormalizeArguments_WithNoArgumentsAndNoAvailableConfiguration_ReturnsEmptyArray()
    {
        var normalizedArguments = Bootstrap.NormalizeArguments(
            [],
            @"C:\missing",
            _ => false,
            () => false);

        Assert.That(normalizedArguments, Is.Empty);
    }

    [Test]
    public void NormalizeArguments_WithConfigurationFileFirst_PrependsRunVerb()
    {
        var normalizedArguments = Bootstrap.NormalizeArguments(["mocker.qaas.yaml"]);

        Assert.That(normalizedArguments, Is.EqualTo(new[] { "run", "mocker.qaas.yaml" }));
    }

    [Test]
    public void NormalizeArguments_WithConfigurationFileAndNoEnv_PrependsRunVerb()
    {
        var normalizedArguments = Bootstrap.NormalizeArguments(["mocker.qaas.yaml", "--no-env"]);

        Assert.That(normalizedArguments, Is.EqualTo(new[] { "run", "mocker.qaas.yaml", "--no-env" }));
    }

    [Test]
    public void NormalizeArguments_WithRelativePathContainingDirectorySeparator_PrependsRunVerb()
    {
        var normalizedArguments = Bootstrap.NormalizeArguments(["configs\\mocker.qaas"]);

        Assert.That(normalizedArguments, Is.EqualTo(new[] { "run", "configs\\mocker.qaas" }));
    }

    [Test]
    public void New_WithLegacyModeFlag_WritesTopLevelHelpAsInvalidCommandLine()
    {
        var output = CaptureConsoleOut(() =>
        {
            var mocker = Bootstrap.New(["--mode", "template", "mocker.qaas.yaml"]);
            Assert.DoesNotThrow(() => mocker.Run());
        });

        Assert.Multiple(() =>
        {
            Assert.That(output, Does.Contain("Verb '--mode' is not recognized."));
            Assert.That(output, Does.Contain("Usage:"));
        });
    }

    [Test]
    public void NormalizeArguments_WithUnknownVerb_LeavesArgumentsUntouched()
    {
        var normalizedArguments = Bootstrap.NormalizeArguments(["serve", "mocker.qaas.yaml"]);

        Assert.That(normalizedArguments, Is.EqualTo(new[] { "serve", "mocker.qaas.yaml" }));
    }

    [Test]
    public void New_WithConfigurationFileOnly_UsesRunModeWithoutPrintingHelp()
    {
        var tempDirectory = CreateTempDirectory();
        try
        {
            var configFile = WriteFile(tempDirectory, "mocker.qaas.yaml", """
                Server:
                  Http:
                    Port: 8443
                """);

            var output = CaptureConsoleOut(() =>
            {
                var mocker = Bootstrap.New([configFile]);
                Assert.That(mocker, Is.Not.Null);
            });

            Assert.That(output, Does.Not.Contain("Usage:"));
        }
        finally
        {
            DeleteDirectory(tempDirectory);
        }
    }

    [Test]
    public void New_WithNullArgsAndDefaultConfigurationInAppBaseDirectory_WritesHelpWithoutRunning()
    {
        var defaultConfigurationPath = Path.Combine(AppContext.BaseDirectory, Constants.DefaultMockerConfigurationFileName);
        var hadExistingFile = File.Exists(defaultConfigurationPath);
        var existingContent = hadExistingFile ? File.ReadAllText(defaultConfigurationPath) : null;

        try
        {
            File.WriteAllText(defaultConfigurationPath, """
                Server:
                  Http:
                    Port: 8443
                """);

            var output = CaptureConsoleOut(() =>
            {
                var runner = Bootstrap.New<TrackingMockerRunner>(null);
                var customRunner = runner as TrackingMockerRunner;

                Assert.That(customRunner, Is.Not.Null);

                customRunner!.Run();

                Assert.Multiple(() =>
                {
                    Assert.That(customRunner.BuildExecutionCalled, Is.False);
                    Assert.That(customRunner.StartExecutionCalled, Is.False);
                    Assert.That(customRunner.ExitProcessCalled, Is.False);
                    Assert.That(customRunner.BootstrapExitCode, Is.Zero);
                });
            });

            Assert.Multiple(() =>
            {
                Assert.That(output, Does.Contain("Usage:"));
                Assert.That(output, Does.Contain("dotnet run -- run <config-file>"));
            });
        }
        finally
        {
            if (hadExistingFile)
                File.WriteAllText(defaultConfigurationPath, existingContent!);
            else if (File.Exists(defaultConfigurationPath))
                File.Delete(defaultConfigurationPath);
        }
    }

    [Test]
    public void New_WithRunVerbOnly_UsesDefaultConfigurationFromAppBaseDirectory()
    {
        var defaultConfigurationPath = Path.Combine(AppContext.BaseDirectory, Constants.DefaultMockerConfigurationFileName);
        var hadExistingFile = File.Exists(defaultConfigurationPath);
        var existingContent = hadExistingFile ? File.ReadAllText(defaultConfigurationPath) : null;

        try
        {
            File.WriteAllText(defaultConfigurationPath, """
                Server:
                  Http:
                    Port: 8443
                """);

            var output = CaptureConsoleOut(() =>
            {
                var runner = Bootstrap.New<TrackingMockerRunner>(["run"]);
                var customRunner = runner as TrackingMockerRunner;

                Assert.That(customRunner, Is.Not.Null);

                customRunner!.Run();

                Assert.Multiple(() =>
                {
                    Assert.That(customRunner.BuildExecutionCalled, Is.True);
                    Assert.That(customRunner.StartExecutionCalled, Is.True);
                    Assert.That(customRunner.ExitProcessCalled, Is.True);
                    Assert.That(customRunner.ObservedExitCode, Is.Zero);
                });
            });

            Assert.That(output, Does.Not.Contain("Usage:"));
        }
        finally
        {
            if (hadExistingFile)
                File.WriteAllText(defaultConfigurationPath, existingContent!);
            else if (File.Exists(defaultConfigurationPath))
                File.Delete(defaultConfigurationPath);
        }
    }

    [Test]
    public void New_WithCustomRunner_ReturnsCustomRunnerAndUsesCustomLifecycle()
    {
        var tempDirectory = CreateTempDirectory();
        try
        {
            var configFile = WriteFile(tempDirectory, "mocker.qaas.yaml", """
                Server:
                  Http:
                    Port: 8443
                """);

            var runner = Bootstrap.New<TrackingMockerRunner>(["template", configFile]);
            var customRunner = runner as TrackingMockerRunner;

            Assert.That(customRunner, Is.Not.Null);

            customRunner!.Run();

            Assert.Multiple(() =>
            {
                Assert.That(runner, Is.TypeOf<TrackingMockerRunner>());
                Assert.That(customRunner.BuildExecutionCalled, Is.True);
                Assert.That(customRunner.StartExecutionCalled, Is.True);
                Assert.That(customRunner.ExitProcessCalled, Is.True);
                Assert.That(customRunner.ObservedExitCode, Is.Zero);
            });
        }
        finally
        {
            DeleteDirectory(tempDirectory);
        }
    }

    [Test]
    public void New_WithNullArgsAndCustomRunner_WritesHelpWithoutInvokingExecutionLifecycle()
    {
        var output = WithDefaultConfigurationFileInAppBaseDirectory(null, () => CaptureConsoleOut(() =>
        {
            var runner = Bootstrap.New<TrackingMockerRunner>(null);
            var customRunner = runner as TrackingMockerRunner;

            Assert.That(customRunner, Is.Not.Null);

            customRunner!.Run();

            Assert.Multiple(() =>
            {
                Assert.That(runner, Is.TypeOf<TrackingMockerRunner>());
                Assert.That(customRunner.BuildExecutionCalled, Is.False);
                Assert.That(customRunner.StartExecutionCalled, Is.False);
                Assert.That(customRunner.ExitProcessCalled, Is.False);
                Assert.That(customRunner.BootstrapExitCode, Is.Zero);
            });
        }));

        Assert.That(output, Does.Contain("Usage:"));
    }

    private static string CaptureConsoleOut(Action action)
    {
        var originalOut = Console.Out;
        var writer = new StringWriter();

        try
        {
            Console.SetOut(writer);
            action();
            return writer.ToString();
        }
        finally
        {
            Console.SetOut(originalOut);
            Environment.ExitCode = 0;
        }
    }

    private static T WithDefaultConfigurationFileInAppBaseDirectory<T>(string? content, Func<T> action)
    {
        var defaultConfigurationPath = Path.Combine(AppContext.BaseDirectory, Constants.DefaultMockerConfigurationFileName);
        var hadExistingFile = File.Exists(defaultConfigurationPath);
        var existingContent = hadExistingFile ? File.ReadAllText(defaultConfigurationPath) : null;

        try
        {
            if (content == null)
            {
                if (File.Exists(defaultConfigurationPath))
                    File.Delete(defaultConfigurationPath);
            }
            else
            {
                File.WriteAllText(defaultConfigurationPath, content);
            }

            return action();
        }
        finally
        {
            if (hadExistingFile)
                File.WriteAllText(defaultConfigurationPath, existingContent!);
            else if (File.Exists(defaultConfigurationPath))
                File.Delete(defaultConfigurationPath);
        }
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

    private sealed class TrackingMockerRunner(IEnumerable<ExecutionBuilder>? executionBuilders,
        Action<int>? exitAction = null)
        : MockerRunner(executionBuilders, exitAction)
    {
        public bool BuildExecutionCalled { get; private set; }
        public bool StartExecutionCalled { get; private set; }
        public bool ExitProcessCalled { get; private set; }
        public int? ObservedExitCode { get; private set; }
        public int? BootstrapExitCode { get; private set; }

        protected override BaseExecution BuildExecution(ExecutionBuilder executionBuilder)
        {
            BuildExecutionCalled = true;
            return base.BuildExecution(executionBuilder);
        }

        protected override int StartExecution(BaseExecution execution)
        {
            StartExecutionCalled = true;
            return 0;
        }

        protected override void ExitProcess(int exitCode)
        {
            ExitProcessCalled = true;
            ObservedExitCode = exitCode;
        }

        protected override void SetProcessExitCode(int exitCode)
        {
            BootstrapExitCode = exitCode;
            base.SetProcessExitCode(exitCode);
        }
    }
}
