namespace QaaS.Mocker.Controller.Controllers;

/// <summary>
/// Represents a long-running control-plane listener for a mocker runtime.
/// </summary>
public interface IController
{
    /// <summary>
    /// Starts the controller listeners and blocks for the controller lifetime.
    /// </summary>
    public void Start();
}
