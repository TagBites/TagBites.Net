namespace TagBites.Net;

/// <summary>
/// Provides data for the <see cref="E:TagBites.Net.NetworkConnection.ControllerResolve"/> event.
/// </summary>
public class NetworkConnectionControllerResolveEventArgs : EventArgs
{
    /// <summary>
    /// Full name of controller to resolve.
    /// </summary>
    public string ControllerTypeName { get; }
    /// <summary>
    /// Type of controller to resolve.
    /// </summary>
    public Type ControllerType { get; }
    /// <summary>
    /// Gets or sets resolved controller.
    /// </summary>
    public object Controller { get; set; }

    internal NetworkConnectionControllerResolveEventArgs(string controllerTypeName, Type controllerType)
    {
        ControllerType = controllerType;
        ControllerTypeName = controllerTypeName;
    }
}
