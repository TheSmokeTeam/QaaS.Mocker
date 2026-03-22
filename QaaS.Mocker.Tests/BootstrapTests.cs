using CommandLine;
using NUnit.Framework;
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
        var output = CaptureConsoleOut(() =>
        {
            var mocker = Bootstrap.New(null);
            Assert.DoesNotThrow(() => mocker.Run());
        });

        Assert.Multiple(() =>
        {
            Assert.That(output, Does.Contain("Usage:"));
            Assert.That(output, Does.Contain("Command Details:"));
            Assert.That(output, Does.Contain("run:"));
            Assert.That(output, Does.Contain("lint:"));
            Assert.That(output, Does.Contain("template:"));
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
            Assert.That(output, Does.Contain("lint:"));
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
            Assert.That(output, Does.Contain("lint:"));
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
            Assert.That(output, Does.Contain("lint:"));
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
            Assert.That(output, Does.Not.Contain("lint:"));
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
            Assert.That(output, Does.Not.Contain("lint:"));
            Assert.That(output, Does.Not.Contain("template:"));
        });
    }

    [Test]
    public void ParserBuilder_ParsesExecutionModeCaseInsensitive()
    {
        using var parser = ParserBuilder.BuildParser();
        var parserResult =
            parser.ParseArguments<RunOptions, LintOptions, TemplateOptions>(["tEmPlAtE", "mocker.qaas.yaml"]);

        var parsedExecutionMode = parserResult.MapResult(
            (RunOptions _) => ExecutionMode.Run,
            (LintOptions _) => ExecutionMode.Lint,
            (TemplateOptions _) => ExecutionMode.Template,
            _ => (ExecutionMode?)null);

        Assert.That(parsedExecutionMode, Is.EqualTo(ExecutionMode.Template));
    }

    [TestCase("run", ExecutionMode.Run)]
    [TestCase("lint", ExecutionMode.Lint)]
    [TestCase("template", ExecutionMode.Template)]
    public void NormalizeArguments_WithVerbAlias_PrependsModeFlag(string verb, ExecutionMode expectedMode)
    {
        var normalizedArguments = Bootstrap.NormalizeArguments([verb, "mocker.qaas.yaml"]);

        Assert.That(normalizedArguments, Is.EqualTo(new[] { verb, "mocker.qaas.yaml" }));

        using var parser = ParserBuilder.BuildParser();
        var parsedExecutionMode = parser.ParseArguments<RunOptions, LintOptions, TemplateOptions>(normalizedArguments)
            .MapResult(
                (RunOptions _) => ExecutionMode.Run,
                (LintOptions _) => ExecutionMode.Lint,
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
        var normalizedArguments = Bootstrap.NormalizeArguments([]);

        Assert.That(normalizedArguments, Is.Empty);
    }

    [Test]
    public void NormalizeArguments_WithConfigurationFileFirst_LeavesArgumentsUntouched()
    {
        var normalizedArguments = Bootstrap.NormalizeArguments(["mocker.qaas.yaml"]);

        Assert.That(normalizedArguments, Is.EqualTo(new[] { "mocker.qaas.yaml" }));
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
}
