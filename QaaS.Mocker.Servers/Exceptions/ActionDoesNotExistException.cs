namespace QaaS.Mocker.Servers.Exceptions;

/// <summary>
/// Thrown when a controller command references an action name that is not registered on the server.
/// </summary>
public class ActionDoesNotExistException(string message) : Exception(message);
