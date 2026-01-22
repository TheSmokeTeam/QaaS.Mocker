namespace QaaS.Mocker.Servers.Exceptions;

/// <summary>
/// Represents an exception that occurs when a fatal internal error happens in the server.
/// </summary>
public class FatalInternalErrorException(string message, Exception innerException) : Exception(message, innerException);