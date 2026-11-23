using Microsoft.Extensions.Logging;
using QaaS.Framework.SDK;
using QaaS.Framework.SDK.ContextObjects;
using QaaS.Framework.SDK.Extensions;

namespace QaaS.Mocker.Servers;

internal sealed record MockerLogIdentity(string ExecutionId, string Team, string System)
{
    internal static MockerLogIdentity? From(Context context)
    {
        try
        {
            if (!context.Logger.IsEnabled(LogLevel.Debug))
                return null;
            MetaDataConfig? metadata = null;
            if (context is InternalContext internalContext)
            {
                try { metadata = internalContext.GetMetaDataFromContext(); }
                catch (KeyNotFoundException) { }
                catch (InvalidOperationException) { }
            }
            var team = string.IsNullOrWhiteSpace(metadata?.Team) ? context.RootConfiguration["MetaData:Team"] : metadata.Team;
            var system = string.IsNullOrWhiteSpace(metadata?.System) ? context.RootConfiguration["MetaData:System"] : metadata.System;
            return string.IsNullOrWhiteSpace(team) || string.IsNullOrWhiteSpace(system)
                ? null
                : new(string.IsNullOrEmpty(context.ExecutionId) ? Guid.NewGuid().ToString("N") : context.ExecutionId,
                    team, system);
        }
        catch (Exception exception) when (exception is not OutOfMemoryException and not AccessViolationException)
        {
            // A custom provider can throw even when checking the log level.
            return null;
        }
    }
}
