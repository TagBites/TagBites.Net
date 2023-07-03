using System.Net;

namespace TagBites.Net;

/// <summary>
/// TCP server client which allows to send objects messages and execute remote methods.
/// This class is thread safe.
/// </summary>
public class ServerClient : NetworkClient
{
    /// <summary>
    /// Gets client identity specified during authorization.
    /// </summary>
    public object Identity { get; }
    /// <summary>
    /// Get server instance that established a connection with this client.
    /// A null value when server was disposed.
    /// </summary>
    public Server Server { get; internal set; }

    /// <summary>
    /// Gets the remote endpoint.
    /// </summary>
    /// <returns>The <see cref="T:System.Net.EndPoint" /> with which the <see cref="T:System.Net.Sockets.Socket" /> is communicating.</returns>
    public EndPoint RemoteEndPoint { get; }

    internal ServerClient(Server server, object identity, NetworkConnection connection)
    {
        Identity = identity;
        Server = server;
        Connection = connection;
        RemoteEndPoint = connection.TcpClient.Client.RemoteEndPoint;
    }


    /// <summary>
    /// Register local controller.
    /// </summary>
    /// <typeparam name="TControllerInterface">Controller interface.</typeparam>
    /// <typeparam name="TController">Controller type.</typeparam>
    public void Use<TControllerInterface, TController>() where TController : TControllerInterface, new()
    {
        Connection.Use<TControllerInterface, TController>();
    }
    /// <summary>
    /// Register local controller.
    /// </summary>
    /// <typeparam name="TControllerInterface">Controller interface.</typeparam>
    /// <typeparam name="TController">Controller type.</typeparam>
    /// <param name="controller">Controller instance.</param>
    public void Use<TControllerInterface, TController>(TController controller) where TController : TControllerInterface
    {
        Connection.Use<TControllerInterface, TController>(controller);
    }

    /// <inheritdoc />
    protected override void OnConnectionClosed(object sender, NetworkConnectionClosedEventArgs e)
    {
        base.OnConnectionClosed(sender, e);
        Server?.OnClientDisconnected(this, e);
    }
    /// <inheritdoc />
    protected override void OnReceived(object sender, NetworkConnectionMessageEventArgs e)
    {
        base.OnReceived(sender, e);
        Server?.OnClientReceived(this, e);
    }
    /// <inheritdoc />
    protected override void OnReceivedError(object sender, NetworkConnectionMessageErrorEventArgs e)
    {
        base.OnReceivedError(sender, e);
        Server?.OnClientReceivedError(this, e);
    }
    /// <inheritdoc />
    protected override void OnControllerResolve(object sender, NetworkConnectionControllerResolveEventArgs e)
    {
        base.OnControllerResolve(sender, e);
        Server?.OnClientControllerResolve(this, e);
    }

    /// <inheritdoc />
    public override string ToString()
    {
        return Identity?.ToString() ?? RemoteEndPoint.ToString();
    }
}
