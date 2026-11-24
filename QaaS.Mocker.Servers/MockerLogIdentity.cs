using System.Reflection;
using Microsoft.Extensions.Logging;
using QaaS.Framework.SDK;
using QaaS.Framework.SDK.ContextObjects;
using QaaS.Framework.SDK.Extensions;
using QaaS.Mocker.Servers.ConfigurationObjects;

namespace QaaS.Mocker.Servers;

internal sealed record MockerAttempt(string Id, string ExecutionType, string Version, string[] Features);

internal sealed record MockerLogIdentity(string ExecutionId, string Team, string System, string Project = "Unknown")
{
    private const string Unknown = "Unknown";
    internal const string Labels = "(ExecutionId: {ExecutionId}, AttemptId: {AttemptId}, Team: {Team}, Project: {Project}, System: {System}, TeamKnown: {TeamKnown}, ProjectKnown: {ProjectKnown}, SystemKnown: {SystemKnown}, ExecutionType: {ExecutionType}, Version: {Version}, Features: {Features})";
    private readonly AsyncLocal<MockerAttempt?> _attempt = new();

    internal static bool Enabled => !string.Equals(Environment.GetEnvironmentVariable("QAAS_USAGE_ENABLED"), "false", StringComparison.OrdinalIgnoreCase);
    internal bool TeamKnown => Team != Unknown;
    internal bool ProjectKnown => Project != Unknown;
    internal bool SystemKnown => System != Unknown;
    internal string[] ServerFeatures { get; private init; } = [];
    internal MockerAttempt? CurrentAttempt { get => _attempt.Value; set => _attempt.Value = value; }

    internal MockerAttempt NewAttempt(bool runLocally, bool controller, string version)
    {
        string[] features = controller ? [.. ServerFeatures, "controller"] : ServerFeatures;
        return new(Guid.NewGuid().ToString("N"), runLocally ? "RunLocally" : "Run", version,
            features.Length == 0 ? ["none"] : features);
    }

    internal static string GetVersion(Assembly assembly)
    {
        var version = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        return version?.Split('+')[0] ?? assembly.GetName().Version?.ToString() ?? Unknown;
    }

    internal static MockerLogIdentity? From(Context context, IReadOnlyList<ServerConfig>? servers = null)
    {
        try
        {
            if (!Enabled || !context.Logger.IsEnabled(LogLevel.Debug))
                return null;
            MetaDataConfig? metadata = null;
            if (context is InternalContext internalContext)
            {
                try { metadata = internalContext.GetMetaDataFromContext(); }
                catch (KeyNotFoundException) { }
                catch (InvalidOperationException) { }
            }
            var config = context.RootConfiguration;
            var team = Label(metadata?.Team, config?["MetaData:Team"]);
            var system = Label(metadata?.System, config?["MetaData:System"]);
            var project = Label(metadata?.ExtraLabels?.GetValueOrDefault("Project") as string, config?["MetaData:ExtraLabels:Project"]);
            var executionId = Guid.TryParse(context.ExecutionId, out var existingId) ? existingId : Guid.NewGuid();
            return new(executionId.ToString("N"), team, system, project) { ServerFeatures = Features(servers) };
        }
        catch (Exception exception) when (exception is not OutOfMemoryException and not AccessViolationException)
        {
            // A custom provider can throw even when checking the log level.
            return null;
        }
    }

    private static string Label(string? configured, string? fallback)
    {
        var value = (string.IsNullOrWhiteSpace(configured) ? fallback : configured)?.Trim();
        return string.IsNullOrEmpty(value) || value.Length > 128 || value.Any(char.IsControl) ||
               string.Equals(value, Unknown, StringComparison.OrdinalIgnoreCase) ? Unknown : value;
    }

    private static string[] Features(IReadOnlyList<ServerConfig>? servers)
    {
        if (servers == null) return [];
        var features = new List<string>(5);
        if (servers.Any(s => s.Http != null)) features.Add("http");
        if (servers.Any(s => s.Http?.IsSecuredSchema == true)) features.Add("https");
        if (servers.Any(s => s.Grpc != null)) features.Add("grpc");
        if (servers.Any(s => s.Socket != null)) features.Add("socket");
        if (servers.Any(s => s.Http?.EnableHttpMetrics == true)) features.Add("http_counters");
        return features.ToArray();
    }
}
