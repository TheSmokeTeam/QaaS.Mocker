using System.IO.Abstractions;
using QaaS.Framework.SDK.ContextObjects;
using QaaS.Mocker.Executions;

namespace QaaS.Mocker.Tests.Mocks;

/// <summary>
/// Mock of the template executor that doesn't require any configuration file to run
/// </summary>
public class TemplateExecutionMock: TemplateExecution
{
    public TemplateExecutionMock(IFileSystem fileSystem) : base(new Context { Logger = Globals.Logger }, "test")
    {
        FileSystem = fileSystem;
    }
}