using System.Collections.Immutable;
using System.ComponentModel.DataAnnotations;
using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
using OpenTelemetry.Metrics;
using QaaS.Framework.SDK.ContextObjects;
using QaaS.Framework.SDK.DataSourceObjects;
using QaaS.Framework.SDK.Hooks.Processor;
using QaaS.Framework.SDK.Session.DataObjects;
using QaaS.Framework.SDK.Session.MetaDataObjects;
using QaaS.Mocker.Servers.ConfigurationObjects.HttpServerConfigs;
using QaaS.Mocker.Servers.Servers;
using QaaS.Mocker.Stubs;
using QaaS.Mocker.Stubs.Stubs;
using Method = QaaS.Mocker.Servers.ConfigurationObjects.HttpServerConfigs.HttpMethod;

var builder = WebApplication.CreateBuilder(Array.Empty<string>());
builder.WebHost.UseUrls("http://127.0.0.1:" + args[1]);
builder.Services.AddOpenTelemetry().WithMetrics(m => m.AddMeter("QaaS.Mocker.Http").AddPrometheusExporter());
var app = builder.Build();
app.MapPrometheusScrapingEndpoint();
// These controls exist only on the test exporter host, never on a Mocker route.
app.MapPost("/probe/duplicate", () => RejectedSlot(1, duplicate: true));
app.MapPost("/probe/invalid", () => RejectedSlot(9, duplicate: false));
await app.StartAsync();

var enabled = args.Length < 5;
var mockerA = CreateServer(int.Parse(args[0]), enabled, 1);
var mockerB = CreateServer(int.Parse(args[2]), enabled, 2);
var disabled = CreateServer(int.Parse(args[3]), enabled: false, slot: 99);
foreach (var server in new[] { mockerA, mockerB, disabled })
    _ = Task.Factory.StartNew(server.Start, TaskCreationOptions.LongRunning);
await app.WaitForShutdownAsync();

static IResult RejectedSlot(int slot, bool duplicate)
{
    try
    {
        _ = CreateServer(0, enabled: true, slot);
        return Results.Problem("Invalid instrumentation configuration unexpectedly accepted.");
    }
    catch (InvalidOperationException) when (duplicate)
    {
        return Results.Conflict("Duplicate metrics slot rejected.");
    }
    catch (ArgumentOutOfRangeException) when (!duplicate)
    {
        return Results.BadRequest("Out-of-range metrics slot rejected.");
    }
}

static HttpServer CreateServer(int port, bool enabled, int slot) => new(new HttpServerConfig
{
    Port = port,
    IsLocalhost = true,
    EnableHttpMetrics = enabled,
    HttpMetricsSlot = slot,
    Endpoints = [
       new HttpEndpointConfig { Path="/matched", Actions=[new HttpEndpointActionConfig {
           Method=Method.Get, Name="Matched", TransactionStubName="configured404" }] },
       new HttpEndpointConfig { Path="/broken", Actions=[new HttpEndpointActionConfig {
           Method=Method.Get, Name="Broken", TransactionStubName="broken" }] },
    ]
}, NullLogger.Instance, [
    Create("configured404",404), Create("broken",0),
    Create(Constants.DefaultNotFoundTransactionStubLabel,200),
    Create(Constants.DefaultInternalErrorTransactionStubLabel,202)
]);
static TransactionStub Create(string name, int status) => new()
{
    Name = name,
    DataSourceList = ImmutableList<DataSource>.Empty,
    Processor = new Processor(status),
};
sealed class Processor(int status) : ITransactionProcessor
{
    public Context Context { get; set; } = null!;
    public List<ValidationResult>? LoadAndValidateConfiguration(IConfiguration configuration) => [];
    public Data<object> Process(IImmutableList<DataSource> sources, Data<object> request)
        => status == 0 ? throw new InvalidOperationException("synthetic")
            : new Data<object>
            {
                Body = Encoding.UTF8.GetBytes(status.ToString()),
                MetaData = new MetaData { Http = new Http { StatusCode = status } }
            };
}
