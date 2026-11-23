using Microsoft.Extensions.Configuration;
using Moq;
using NUnit.Framework;
using QaaS.Framework.SDK.ContextObjects;
using QaaS.Framework.SDK;
using QaaS.Framework.SDK.Extensions;
using QaaS.Mocker.Controller.Controllers;
using QaaS.Mocker.Logics;
using QaaS.Mocker.Options;
using QaaS.Mocker.Servers.Servers;

namespace QaaS.Mocker.Tests.ExecutionTests;

[TestFixture]
public class ExecutionBranchTests
{
    [Test]
    public void Run_UsesCodeFirstOwnershipWithYamlFallbackForMissingLabels()
    {
        var logger = new Mock<Microsoft.Extensions.Logging.ILogger>();
        logger.Setup(x => x.IsEnabled(Microsoft.Extensions.Logging.LogLevel.Debug)).Returns(true);
        var context = new InternalContext
        {
            Logger = logger.Object,
            RootConfiguration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
                { ["MetaData:Team"] = "yaml-team", ["MetaData:System"] = "orders" }).Build()
        };
        context.InsertValueIntoGlobalDictionary(context.GetMetaDataPath(), new MetaDataConfig { Team = "sdk-team" });
        Assert.That(CreateExecution(ExecutionMode.Run, context).Start(), Is.Zero);
        var events = logger.Invocations.Where(x => x.Method.Name == "Log")
            .Select(x => ((IEnumerable<KeyValuePair<string, object?>>)x.Arguments[2]).ToDictionary())
            .Where(x => x.ContainsKey("QaasEvent")).ToArray();
        Assert.That(events, Has.Length.EqualTo(2));
        Assert.That(events.All(x => Equals(x["Team"], "sdk-team") && Equals(x["System"], "orders")), Is.True);
    }

    [TestCase(ExecutionMode.Template, true, "orders")]
    [TestCase(ExecutionMode.Run, false, "orders")]
    [TestCase(ExecutionMode.Run, true, " ")]
    public void Start_ExcludesTemplatesDisabledDebugAndMissingOwners(ExecutionMode mode, bool debug, string system)
    {
        var logger = new Mock<Microsoft.Extensions.Logging.ILogger>();
        logger.Setup(x => x.IsEnabled(Microsoft.Extensions.Logging.LogLevel.Debug)).Returns(debug);
        var context = new Context
        {
            Logger = logger.Object,
            RootConfiguration = new ConfigurationBuilder().AddInMemoryCollection(
                new Dictionary<string, string?> { ["MetaData:Team"] = "red", ["MetaData:System"] = system }).Build()
        };
        var builder = new Mock<ExecutionBuilder>();
        builder.Setup(x => x.Build()).Returns(CreateExecution(mode, context));
        var drained = false;
        var runner = new MockerRunner([builder.Object], code => Assert.That(code, Is.Zero))
        {
            DrainCliLogger = () => drained = true
        };
        runner.Run();
        Assert.That(drained, Is.False);
        var messages = logger.Invocations.Where(x => x.Method.Name == "Log")
            .Select(x => ((IEnumerable<KeyValuePair<string, object?>>)x.Arguments[2]).ToDictionary()).ToList();
        Assert.That(messages.Any(x => x.ContainsKey("QaasEvent")), Is.False);
        Assert.That(messages.Any(x => x["{OriginalFormat}"]?.ToString() == "Starting QaaS.Mocker in {ExecutionMode} mode (RunLocally: {RunLocally})"), Is.True);
    }

    [TestCase(false)]
    [TestCase(true)]
    public void Run_LogsOneScopedStartAndTerminalOutcome(bool fail)
    {
        var logger = new Mock<Microsoft.Extensions.Logging.ILogger>();
        logger.Setup(x => x.IsEnabled(It.IsAny<Microsoft.Extensions.Logging.LogLevel>())).Returns(true);
        var context = new Context
        {
            Logger = logger.Object,
            RootConfiguration = new ConfigurationBuilder().AddInMemoryCollection(
                new Dictionary<string, string?> { ["MetaData:Team"] = "red", ["MetaData:System"] = "orders" }).Build()
        };
        var server = new Mock<IServer>();
        if (fail)
            server.Setup(x => x.Start()).Throws(new InvalidOperationException("original failure"));
        var execution = CreateExecution(ExecutionMode.Run, context, new ServerLogic(server.Object));
        if (fail)
            Assert.That(() => execution.Start(), Throws.TypeOf<AggregateException>());
        else
            Assert.That(execution.Start(), Is.Zero);

        var events = logger.Invocations.Where(x => x.Method.Name == "Log")
            .Select(x => ((IEnumerable<KeyValuePair<string, object?>>)x.Arguments[2]).ToDictionary())
            .Where(x => x.ContainsKey("QaasEvent")).ToArray();
        Assert.That(events.Select(x => x["QaasEvent"]),
            Is.EqualTo(new[] { "MockerExecutionStarted", "MockerExecutionFinished" }));
        Assert.Multiple(() =>
        {
            Assert.That(events[1]["Failed"], Is.EqualTo(fail ? 1 : 0));
            Assert.That(events.All(x => Equals(x["Team"], "red") && Equals(x["System"], "orders")), Is.True);
            Assert.That(events[0]["ExecutionId"], Is.Not.Null.And.Not.Empty);
            Assert.That(events[1]["ExecutionId"], Is.EqualTo(events[0]["ExecutionId"]));
        });
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

    [Test]
    public void Dispose_DoesNotThrow()
    {
        var execution = CreateExecution(ExecutionMode.Run);

        Assert.DoesNotThrow(() => execution.Dispose());
    }

    [Test]
    public void SystemExecutionConsole_ReportsConsoleRedirectState()
    {
        Assert.That(SystemExecutionConsole.Instance.IsInputRedirected, Is.EqualTo(Console.IsInputRedirected));
    }

    [Test]
    public void SystemExecutionConsole_ReadKey_WithRedirectedInput_ThrowsInvalidOperationException()
    {
        if (!Console.IsInputRedirected)
            Assert.Ignore("Console.ReadKey only throws predictably under redirected input.");

        Assert.Throws<InvalidOperationException>(() => SystemExecutionConsole.Instance.ReadKey(intercept: true));
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
