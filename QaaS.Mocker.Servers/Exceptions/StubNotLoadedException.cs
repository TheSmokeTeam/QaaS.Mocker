namespace QaaS.Mocker.Servers.Exceptions;

/// <summary>
/// Thrown when configuration references a transaction stub that was not built into the runtime.
/// </summary>
public class StubNotLoadedException(string message) : Exception(message);
