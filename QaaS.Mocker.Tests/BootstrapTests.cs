using CommandLine;
using NUnit.Framework;
using QaaS.Mocker.CommandLineBuilders;
using QaaS.Mocker.Options;

namespace QaaS.Mocker.Tests;

[TestFixture]
public class BootstrapTests
{
    [Test]
    public void New_WithNullArgs_ReturnsNoOpMocker()
    {
        var mocker = Bootstrap.New(null);

        Assert.DoesNotThrow(() => mocker.Run());
    }

    [Test]
    public void New_WithInvalidArgs_ReturnsNoOpMocker()
    {
        var mocker = Bootstrap.New(["--unknown-option"]);

        Assert.DoesNotThrow(() => mocker.Run());
    }

    [Test]
    public void New_WithVersionOption_ReturnsNoOpMocker()
    {
        var mocker = Bootstrap.New(["--version"]);

        Assert.DoesNotThrow(() => mocker.Run());
    }

    [Test]
    public void ParserBuilder_ParsesExecutionModeCaseInsensitive()
    {
        using var parser = ParserBuilder.BuildParser();
        var parserResult = parser.ParseArguments<MockerOptions>(["--mode", "tEmPlAtE", "mocker.qaas.yaml"]);

        var parsedExecutionMode = parserResult is Parsed<MockerOptions> parsed
            ? parsed.Value.ExecutionMode
            : null;

        Assert.That(parsedExecutionMode, Is.EqualTo(ExecutionMode.Template));
    }

    [TestCase("run", ExecutionMode.Run)]
    [TestCase("lint", ExecutionMode.Lint)]
    [TestCase("template", ExecutionMode.Template)]
    public void NormalizeArguments_WithVerbAlias_PrependsModeFlag(string verb, ExecutionMode expectedMode)
    {
        var normalizedArguments = Bootstrap.NormalizeArguments([verb, "mocker.qaas.yaml"]);

        Assert.That(normalizedArguments, Is.EqualTo(new[] { "--mode", verb, "mocker.qaas.yaml" }));

        using var parser = ParserBuilder.BuildParser();
        var parserResult = parser.ParseArguments<MockerOptions>(normalizedArguments);
        var parsedOptions = parserResult.Value;

        Assert.Multiple(() =>
        {
            Assert.That(parsedOptions.ExecutionMode, Is.EqualTo(expectedMode));
            Assert.That(parsedOptions.ConfigurationFile, Is.EqualTo("mocker.qaas.yaml"));
        });
    }

    [Test]
    public void NormalizeArguments_WithOptionFirst_LeavesArgumentsUntouched()
    {
        var normalizedArguments = Bootstrap.NormalizeArguments(["--mode", "run", "mocker.qaas.yaml"]);

        Assert.That(normalizedArguments, Is.EqualTo(new[] { "--mode", "run", "mocker.qaas.yaml" }));
    }
}
