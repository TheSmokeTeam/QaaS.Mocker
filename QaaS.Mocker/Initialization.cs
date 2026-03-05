namespace QaaS.Mocker;

/// <summary>
/// Backward-compatible initializer that delegates to <see cref="Bootstrap"/>.
/// </summary>
public static class Initialization
{
    /// <summary>
    /// Initializes and runs the mocker with the passed arguments.
    /// </summary>
    public static void Initialize(IEnumerable<string> args)
    {
        Bootstrap.New(args).Run();
    }
}
