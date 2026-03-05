using Microsoft.Extensions.Configuration;
using NUnit.Framework;
using QaaS.Framework.SDK.ContextObjects;
using QaaS.Framework.SDK.ExecutionObjects;
using QaaS.Mocker.Logics;
using System.IO.Abstractions.TestingHelpers;

namespace QaaS.Mocker.Tests;

[TestFixture]
public class TemplateLogicTests
{
    [Test]
    public void ShouldRun_ReturnsTrueOnlyForTemplateExecutionType()
    {
        var logic = new TemplateLogic(CreateContext(), writer: TextWriter.Null);

        Assert.Multiple(() =>
        {
            Assert.That(logic.ShouldRun(ExecutionType.Template), Is.True);
            Assert.That(logic.ShouldRun(ExecutionType.Run), Is.False);
        });
    }

    [Test]
    public void Run_WithoutOutputFolder_WritesTemplateToWriter()
    {
        using var writer = new StringWriter();
        var logic = new TemplateLogic(CreateContext(), writer: writer);

        var result = logic.Run(new ExecutionData());

        Assert.Multiple(() =>
        {
            Assert.That(result, Is.Not.Null);
            Assert.That(writer.ToString(), Does.Contain("Server:"));
        });
    }

    [Test]
    public void Run_WithOutputFolder_WritesTemplateFile()
    {
        var fileSystem = new MockFileSystem();
        const string outputFolder = "templates";
        var logic = new TemplateLogic(CreateContext(), outputFolder, fileSystem, TextWriter.Null);
        var expectedPath = fileSystem.Path.Combine(Environment.CurrentDirectory, outputFolder, "template.qaas.yaml");

        _ = logic.Run(new ExecutionData());

        Assert.That(fileSystem.File.Exists(expectedPath), Is.True);
    }

    private static Context CreateContext()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Server:Type"] = "Http",
                ["Server:Http:Port"] = "8080"
            })
            .Build();

        return new Context
        {
            Logger = Globals.Logger,
            RootConfiguration = configuration
        };
    }
}
