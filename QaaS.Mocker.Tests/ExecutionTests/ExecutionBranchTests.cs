using Microsoft.Extensions.Configuration;
using Moq;
using NUnit.Framework;
using QaaS.Framework.SDK.ContextObjects;
using QaaS.Mocker.Controller.Controllers;
using QaaS.Mocker.Logics;
using QaaS.Mocker.Options;
using QaaS.Mocker.Servers.Servers;

namespace QaaS.Mocker.Tests.ExecutionTests;

[TestFixture]
public class ExecutionBranchTests
{
    [Test]
    public void Start_WithLintMode_ReturnsZero()
    {
        var execution = CreateExecution(ExecutionMode.Lint);

        var result = execution.Start();

        Assert.That(result, Is.EqualTo(0));
    }

    [Test]
    public void Start_WithTemplateMode_WritesTemplateToRequestedFolder()
    {
        var tempFolder = Path.Combine("BuildOutput", "TemplateTests", Guid.NewGuid().ToString("N"));
        var context = CreateContext(new Dictionary<string, string?>
        {
            ["Server:Type"] = "Http",
            ["Server:Http:Port"] = "8080"
        });
        var execution = CreateExecution(ExecutionMode.Template, context,
            templateLogic: new TemplateLogic(context, tempFolder));

        var result = execution.Start();

        var templatePath = Path.Combine(Environment.CurrentDirectory, tempFolder, "template.qaas.yaml");
        Assert.Multiple(() =>
        {
            Assert.That(result, Is.EqualTo(0));
            Assert.That(File.Exists(templatePath), Is.True);
            Assert.That(File.ReadAllText(templatePath), Does.Contain("Server:"));
        });

        Directory.Delete(Path.Combine(Environment.CurrentDirectory, tempFolder), recursive: true);
    }

    [Test]
    public void Start_WithRunMode_AndNoController_StartsOnlyServer()
    {
        var server = new Mock<IServer>();
        server.Setup(s => s.Start());
        var serverLogic = new ServerLogic(server.Object);

        var execution = CreateExecution(ExecutionMode.Run, serverLogic: serverLogic, controllerLogic: null);

        var result = execution.Start();

        Assert.That(result, Is.EqualTo(0));
        server.Verify(s => s.Start(), Times.Once);
    }

    [Test]
    public void Start_WithRunMode_AndController_StartsBothServerAndController()
    {
        var server = new Mock<IServer>();
        server.Setup(s => s.Start());
        var controller = new Mock<IController>();
        controller.Setup(c => c.Start());

        var execution = CreateExecution(
            ExecutionMode.Run,
            serverLogic: new ServerLogic(server.Object),
            controllerLogic: new ControllerLogic(controller.Object));

        var result = execution.Start();

        Assert.Multiple(() =>
        {
            Assert.That(result, Is.EqualTo(0));
            server.Verify(s => s.Start(), Times.Once);
            controller.Verify(c => c.Start(), Times.Once);
        });
    }

    [Test]
    public void Start_WithUnsupportedMode_ThrowsArgumentOutOfRangeException()
    {
        var execution = CreateExecution((ExecutionMode)999);

        Assert.Throws<ArgumentOutOfRangeException>(() => execution.Start());
    }

    private static Execution CreateExecution(
        ExecutionMode mode,
        Context? context = null,
        ServerLogic? serverLogic = null,
        ControllerLogic? controllerLogic = null,
        TemplateLogic? templateLogic = null)
    {
        context ??= CreateContext();

        serverLogic ??= new ServerLogic(new Mock<IServer>().Object);
        templateLogic ??= new TemplateLogic(context, writer: TextWriter.Null);

        return new Execution(mode, context, runLocally: false)
        {
            ServerLogic = serverLogic,
            ControllerLogic = controllerLogic,
            TemplateLogic = templateLogic
        };
    }

    private static Context CreateContext(IDictionary<string, string?>? values = null)
    {
        var configuration = values == null
            ? new ConfigurationBuilder().Build()
            : new ConfigurationBuilder().AddInMemoryCollection(values).Build();

        return new Context
        {
            Logger = Globals.Logger,
            RootConfiguration = configuration
        };
    }
}
