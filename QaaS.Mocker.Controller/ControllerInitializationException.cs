namespace QaaS.Mocker.Controller;

public sealed class ControllerInitializationException(string message, Exception innerException)
    : Exception(message, innerException);
