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
    public void Start_WithRunMode_AndRunLocallyUnderRedirectedInput_ReturnsZeroWithoutBlocking()
    {
        var server = new Mock<IServer>();
        server.Setup(s => s.Start());
        var executionConsole = new TestExecutionConsole { IsInputRedirected = true };

        var context = CreateContext();
        var redirectedExecution = CreateExecution(
            ExecutionMode.Run,
            context,
            serverLogic: new ServerLogic(server.Object),
            controllerLogic: null,
            runLocally: true,
            executionConsole: executionConsole);

        var result = redirectedExecution.Start();

        Assert.Multiple(() =>
        {
            Assert.That(result, Is.EqualTo(0));
            Assert.That(executionConsole.ReadKeyCallCount, Is.EqualTo(0));
            server.Verify(s => s.Start(), Times.Once);
        });
    }

    [Test]
    public void Start_WithRunMode_AndRunLocallyUnderInteractiveInput_ReadsKeyAndReturnsZero()
    {
        var server = new Mock<IServer>();
        server.Setup(s => s.Start());
        var executionConsole = new TestExecutionConsole { IsInputRedirected = false };

        var execution = CreateExecution(
            ExecutionMode.Run,
            serverLogic: new ServerLogic(server.Object),
            controllerLogic: null,
            runLocally: true,
            executionConsole: executionConsole);

        var result = execution.Start();

        Assert.Multiple(() =>
        {
            Assert.That(result, Is.EqualTo(0));
            Assert.That(executionConsole.ReadKeyCallCount, Is.EqualTo(1));
            server.Verify(s => s.Start(), Times.Once);
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
        TemplateLogic? templateLogic = null,
        bool runLocally = false,
        IExecutionConsole? executionConsole = null)
    {
        context ??= CreateContext();

        serverLogic ??= new ServerLogic(new Mock<IServer>().Object);
        templateLogic ??= new TemplateLogic(context, writer: TextWriter.Null);

        return new Execution(mode, context, runLocally, executionConsole)
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

    private sealed class TestExecutionConsole : IExecutionConsole
    {
        public bool IsInputRedirected { get; init; }

        public int ReadKeyCallCount { get; private set; }

        public ConsoleKeyInfo ReadKey(bool intercept)
        {
            ReadKeyCallCount++;
            return new ConsoleKeyInfo('x', ConsoleKey.X, shift: false, alt: false, control: false);
        }
    }
}
