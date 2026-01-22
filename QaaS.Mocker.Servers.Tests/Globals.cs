using Microsoft.Extensions.Configuration;
using QaaS.Framework.SDK.ContextObjects;
using Serilog;
using Serilog.Extensions.Logging;
using ILogger = Microsoft.Extensions.Logging.ILogger;

namespace QaaS.Mocker.Servers.Tests;

public static class Globals
{
    public static readonly ILogger Logger = new SerilogLoggerFactory(
        new LoggerConfiguration().MinimumLevel.Debug()
            .WriteTo.NUnitOutput()
            .CreateLogger()).CreateLogger("TestsLogger");
    
    public static readonly Context Context = new()
    {
        Logger = Logger, RootConfiguration = new ConfigurationBuilder().Build()
    };}