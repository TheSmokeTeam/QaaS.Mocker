using System.Collections.Immutable;
using QaaS.Framework.SDK.DataSourceObjects;
using QaaS.Framework.SDK.Hooks.Generator;
using QaaS.Framework.SDK.Session.DataObjects;
using QaaS.Framework.SDK.Session.SessionDataObjects;

namespace QaaS.Mocker.Example.Generators;

public sealed class StaticSocketBroadcastGenerator : BaseGenerator<StaticSocketBroadcastGeneratorConfig>
{
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

public sealed record StaticSocketBroadcastGeneratorConfig
{
    public string[] Messages { get; init; } = ["socket-broadcast-default"];
}
