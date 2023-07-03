namespace TagBites.Net;

/// <summary>
/// Provides data for the <see cref="E:TagBites.Net.Server.ControllerResolve"/> event.
/// </summary>
public class ServerClientControllerResolveEventArgs : ServerClientEventArgs
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

    internal ServerClientControllerResolveEventArgs(ServerClient client, string controllerTypeName, Type controllerType)
        : base(client)
    {
        ControllerType = controllerType;
        ControllerTypeName = controllerTypeName;
    }
}
