namespace QaaS.Mocker.Controller;

/// <summary>
/// Represents unexpected controller initialization failures that are not simple connectivity issues.
/// </summary>
public sealed class ControllerInitializationException(string message, Exception innerException)
    : Exception(message, innerException);
