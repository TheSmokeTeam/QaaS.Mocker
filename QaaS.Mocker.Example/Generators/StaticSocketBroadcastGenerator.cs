using System.Collections.Immutable;
using QaaS.Framework.SDK.DataSourceObjects;
using QaaS.Framework.SDK.Hooks.Generator;
using QaaS.Framework.SDK.Session.DataObjects;
using QaaS.Framework.SDK.Session.SessionDataObjects;

namespace QaaS.Mocker.Example.Generators;

/// <summary>
/// Emits a fixed sequence of UTF-8 socket payloads for the sample broadcast endpoint.
/// </summary>
public sealed class StaticSocketBroadcastGenerator : BaseGenerator<StaticSocketBroadcastGeneratorConfig>
{
    /// <summary>
    /// Generates configured messages or a single default payload when no messages are configured.
    /// </summary>
    public override IEnumerable<Data<object>> Generate(
        IImmutableList<SessionData> sessionDataList,
        IImmutableList<DataSource> dataSourceList)
    {
        var messages = Configuration.Messages is { Length: > 0 }
            ? Configuration.Messages
            : ["socket-broadcast-default"];

        foreach (var message in messages)
        {
            yield return new Data<object>
            {
                Body = System.Text.Encoding.UTF8.GetBytes(message)
            };
        }
    }
}

/// <summary>
/// Configuration for <see cref="StaticSocketBroadcastGenerator"/>.
/// </summary>
public sealed record StaticSocketBroadcastGeneratorConfig
{
    /// <summary>
    /// Gets the messages that should be broadcast by the sample socket endpoint.
    /// </summary>
    public string[] Messages { get; init; } = ["socket-broadcast-default"];
}
