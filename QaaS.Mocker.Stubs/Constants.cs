namespace QaaS.Mocker.Stubs;

/// <summary>
/// Default transaction stub names and status codes used by every built runtime.
/// </summary>
public static class Constants
{
    public const string DefaultNotFoundTransactionStubLabel = "DefaultNotFoundTransaction";
    public const int DefaultNotFoundTransactionStubStatusCode = 404;

    public const string DefaultInternalErrorTransactionStubLabel = "DefaultInternalErrorTransaction";
    public const int DefaultInternalErrorTransactionStubStatusCode = 500;
}
