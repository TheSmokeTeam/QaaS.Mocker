namespace QaaS.Mocker;

public static class Constants
{
    public const string DefaultMockerConfigurationFileName = "mocker.qaas.yaml";
    /// <summary>
    /// List of known names for all QaaS Runner's configurations sections
    /// </summary>
    public static readonly List<string> ConfigurationSectionNames =
    [
        "DataSources",
        "Stubs",
        "Controller",
        "Server",
        "Servers",
    ];
}
