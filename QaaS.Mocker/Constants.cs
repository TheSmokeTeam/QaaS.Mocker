namespace QaaS.Mocker;

/// <summary>
/// Holds shared constants used across CLI parsing and configuration binding.
/// </summary>
public static class Constants
{
    /// <summary>
    /// Default configuration file name used when the CLI positional argument is omitted.
    /// </summary>
    public const string DefaultMockerConfigurationFileName = "mocker.qaas.yaml";

    /// <summary>
    /// List of top-level mocker configuration sections that may be overridden from environment variables.
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
