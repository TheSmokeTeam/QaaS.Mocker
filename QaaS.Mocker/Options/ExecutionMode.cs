namespace QaaS.Mocker.Options;

/// <summary>
/// Defines the supported QaaS.Mocker execution modes.
/// </summary>
public enum ExecutionMode
{
    /// <summary>
    /// Runs the configured mock servers and optional controller.
    /// </summary>
    Run,

    /// <summary>
    /// Writes the effective configuration as a template.
    /// </summary>
    Template
}
