using System.IO;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;

namespace TagBites.Net;

/// <summary>
/// TCP server which allows to send objects messages and execute remote methods.
/// This class is thread safe.
/// </summary>
public class Server : IDisposable
{
    /// <summary>
    /// Occurs right after tcp connection has been established, but before <see cref="ClientConnected"/> event.
    /// It allows to authenticate client and assign identity.
    /// </summary>
    public event EventHandler<ServerClientAuthenticateEventArgs> ClientAuthenticate;
    /// <summary>
    /// Occurs when an exception is thrown while accepting tcp client, or during authentication procedure, or during <see cref="ClientConnected"/> event. 
    /// </summary>
    public event EventHandler<ServerClientConnectExceptionEventArgs> ClientConnectingError;
    /// <summary>
    /// Occurs after the connection is established and client is successfully authenticated.
    /// </summary>
    public event EventHandler<ServerClientEventArgs> ClientConnected;
    /// <summary>
    /// Occurs when client disconnects form the server.
    /// </summary>
    public event EventHandler<ServerClientEventArgs> ClientDisconnected;
    /// <summary>
    /// Occurs when client sends a message.
    /// </summary>
    public event EventHandler<ServerClientMessageEventArgs> Received;
    /// <summary>
    /// Occurs when server was unable to receive client message (eg. deserialization error).
    /// </summary>
    public event EventHandler<ServerClientMessageErrorEventArgs> ReceivedError;
    /// <summary>
    /// Occurs when client requests access to controller for the first time.
    /// </summary>
    public event EventHandler<ServerClientControllerResolveEventArgs> ControllerResolve;

    private readonly NetworkConfig _config;
    private readonly X509Certificate _sslCertificate;
    private TcpListener _listener;
    private bool _listening;
    private Task _listeningTask;
    private readonly List<ServerClient> _clients = new();
    private readonly Dictionary<string, object> _controllers = new();

    /// <summary>
    /// Gets or sets a value indicating whether to dispose connected clients when server is disposing.
    /// </summary>
    public bool DisconnectClientsOnDispose { get; set; } = true;

    /// <summary>
    /// Gets a value indicating whether object already disposed or not.
    /// </summary>
    public bool IsDisposed { get; private set; }
    /// <summary>
    /// Gets or sets a value indicating whether server is listening for clients.
    /// Setting <c>true</c> starts background thread.
    /// </summary>
    public bool Listening
    {
        get => _listening;
        set
        {
            ThrowIfDisposed();

            if (_listening != value)
            {
                _listening = value;

                if (_listening)
                {
                    _listeningTask = _listeningTask != null
                        ? _listeningTask.ContinueWith(t => ListeningTask())
                        : Task.Run(ListeningTask);
                }
                else
                {
                    try { _listener?.Stop(); }
                    catch { /* ignored */ }
                }
            }
        }
    }
    /// <summary>
    /// Gets an address on which server is listening for clients.
    /// </summary>
    public IPEndPoint LocalEndpoint => (IPEndPoint)_listener.LocalEndpoint;

    /// <summary>
    /// Initializes a new instance of the <see cref="Server"/> class.
    /// </summary>
    /// <param name="host">Address on which server is listening for clients.</param>
    /// <param name="port">Port on which server is listening for clients.</param>
    public Server(string host, int port)
        : this(host, port, null, null)
    { }
    /// <summary>
    /// Initializes a new instance of the <see cref="Server"/> class.
    /// </summary>
    /// <param name="host">Address on which server is listening for clients.</param>
    /// <param name="port">Port on which server is listening for clients.</param>
    /// <param name="config">Network configuration.</param>
    public Server(string host, int port, NetworkConfig config)
        : this(host, port, null, config)
    { }
    /// <summary>
    /// Initializes a new instance of the <see cref="Server"/> class.
    /// </summary>
    /// <param name="host">Address on which server is listening for clients.</param>
    /// <param name="port">Port on which server is listening for clients.</param>
    /// <param name="certificate">Certificate used to secure connection (ssl).</param>
    public Server(string host, int port, X509Certificate certificate)
        : this(host, port, certificate, null)
    { }
    /// <summary>
    /// Initializes a new instance of the <see cref="Server"/> class.
    /// </summary>
    /// <param name="host">Address on which server is listening for clients.</param>
    /// <param name="port">Port on which server is listening for clients.</param>
    /// <param name="certificate">Certificate used to secure connection (ssl).</param>
    /// <param name="config">Network configuration.</param>
    public Server(string host, int port, X509Certificate certificate, NetworkConfig config)
        : this(new IPEndPoint(IPAddress.Parse(host), port), certificate, config)
    { }
    /// <summary>
    /// Initializes a new instance of the <see cref="Server"/> class.
    /// </summary>
    /// <param name="address">Address on which server is listening for clients.</param>
    public Server(IPEndPoint address)
        : this(address, null, null)
    { }
    /// <summary>
    /// Initializes a new instance of the <see cref="Server"/> class.
    /// </summary>
    /// <param name="address">Address on which server is listening for clients.</param>
    /// <param name="config">Network configuration.</param>
    public Server(IPEndPoint address, NetworkConfig config)
        : this(address, null, config)
    { }
    /// <summary>
    /// Initializes a new instance of the <see cref="Server"/> class.
    /// </summary>
    /// <param name="address">Address on which server is listening for clients.</param>
    /// <param name="certificate">Certificate used to secure connection (ssl).</param>
    public Server(IPEndPoint address, X509Certificate certificate)
        : this(address, certificate, null)
    { }
    /// <summary>
    /// Initializes a new instance of the <see cref="Server"/> class.
    /// </summary>
    /// <param name="address">Address on which server is listening for clients.</param>
    /// <param name="certificate">Certificate used to secure connection (ssl).</param>
    /// <param name="config">Network configuration.</param>
    public Server(IPEndPoint address, X509Certificate certificate, NetworkConfig config)
    {
        if (address == null)
            throw new ArgumentNullException(nameof(address));

        _config = config ?? NetworkConfig.Default;
        _listener = new TcpListener(address);
        _sslCertificate = certificate;
    }
    /// <inheritdoc />
    ~Server()
    {
        DisposeCore(false);
    }


    /// <summary>
    /// Register local controller.
    /// </summary>
    /// <typeparam name="TControllerInterface">Controller interface.</typeparam>
    /// <typeparam name="TController">Controller type.</typeparam>
    public void Use<TControllerInterface, TController>() where TController : TControllerInterface, new()
    {
        var controllerType = typeof(TControllerInterface);
        var name = controllerType.FullName + ", " + controllerType.Assembly.GetName().Name;

        lock (_controllers)
            _controllers[name] = typeof(TController);
    }
    /// <summary>
    /// Register local controller.
    /// </summary>
    /// <typeparam name="TControllerInterface">Controller interface.</typeparam>
    /// <typeparam name="TController">Controller type.</typeparam>
    /// <param name="controllerProvider">Controller instance.</param>
    public void Use<TControllerInterface, TController>(Func<ServerClient, TController> controllerProvider) where TController : TControllerInterface
    {
        if (controllerProvider == null)
            throw new ArgumentNullException(nameof(controllerProvider));

        var controllerType = typeof(TControllerInterface);
        var name = controllerType.FullName + ", " + controllerType.Assembly.GetName().Name;

        lock (_controllers)
            _controllers[name] = (Func<ServerClient, object>)(x => controllerProvider(x));
    }

    /// <summary>
    /// Returns connected clients.
    /// </summary>
    public ServerClient[] GetClients()
    {
        lock (_clients)
            return _clients.ToArray();
    }
    /// <summary>
    /// Returns connected client with the given <paramref name="identity"/>.
    /// </summary>
    /// <param name="identity">Client identity.</param>
    public ServerClient GetClient(object identity)
    {
        if (identity == null)
            throw new ArgumentNullException(nameof(identity));

        lock (_clients)
            return _clients.FirstOrDefault(x => x.Identity != null && x.Identity.Equals(identity));
    }

    /// <summary>
    /// Sends message to all clients.
    /// </summary>
    /// <param name="message">Message to send.</param>
    public Task SendToAllAsync(object message) => SendToAllAsync(message, null);
    /// <summary>
    /// Sends message to all clients except given one (<paramref name="exceptClient"/>).
    /// </summary>
    /// <param name="message">Message to send.</param>
    /// <param name="exceptClient">Client to whom do not send message.</param>
    public async Task SendToAllAsync(object message, ServerClient exceptClient)
    {
        ThrowIfDisposed();

        List<Exception> ex = null;

        foreach (var client in GetClients())
            if (client.Server == this && client != exceptClient)
                try
                {
                    await client.SendAsync(message);
                }
                catch (Exception e) when (!(e is NetworkConnectionBreakException) && !(e is ObjectDisposedException))
                {
                    if (ex == null)
                        ex = new List<Exception>();

                    ex.Add(e);
                }

        if (ex != null)
            throw new AggregateException("Error occurred while sending message to all clients.", ex);
    }

    private async void ListeningTask()
    {
        _listener.Start();

        while (Listening)
        {
            try
            {
                var tcpClient = await _listener.AcceptTcpClientAsync();

#pragma warning disable 4014
                // ReSharper disable once MethodSupportsCancellation
                Task.Run(() => ProcessClient(tcpClient));
#pragma warning restore 4014
            }
            catch (Exception ex)
            {
                if (Listening)
                    ClientConnectingError?.Invoke(this, new ServerClientConnectExceptionEventArgs(ex));

                break;
            }
        }

        try { _listener?.Stop(); }
        catch { /* ignored */ }
    }
    private async void ProcessClient(TcpClient tcpClient)
    {
        Stream stream = null;
        NetworkConnection connection = null;
        var closeConnection = true;

        try
        {
            // Create connection
            if (_sslCertificate == null)
            {
                var networkStream = tcpClient.GetStream();
                //networkStream.ReadTimeout = ReceiveTimeOut;
                //networkStream.WriteTimeout = SendTimeOut;

                stream = networkStream;
                connection = new NetworkConnection(_config, tcpClient, networkStream);
            }
            else
            {
                var sslStream = new SslStream(tcpClient.GetStream(), false);
                //sslStream.ReadTimeout = ReceiveTimeOut;
                //sslStream.WriteTimeout = SendTimeOut;
                var sslProtocols = SslProtocols.Tls12;
#if NET7_0_OR_GREATER
                sslProtocols |= SslProtocols.Tls13;
#endif

                await sslStream.AuthenticateAsServerAsync(_sslCertificate, false, sslProtocols, true).ConfigureAwait(false);

                stream = sslStream;
                connection = new NetworkConnection(_config, tcpClient, sslStream);
            }

            // Authenticate
            object identity = null;
            var auth = await connection.ReadAsync();
            {
                var credentials = auth as ClientCredentials;

                if (auth != null && credentials == null)
                    throw new ClientAuthenticationException();

                var ca = ClientAuthenticate;
                if (ca != null)
                {
                    var e = new ServerClientAuthenticateEventArgs(credentials);
                    try
                    {
                        ca(this, e);
                    }
                    catch (Exception ex2)
                    {
                        throw new ClientAuthenticationException(ex2);
                    }

                    if (!e.Authenticated)
                        throw new ClientAuthenticationException();

                    identity = e.Identity ?? e.Credentials.UserName;
                }
            }
            await connection.WriteAsync(true);

            // Create
            var client = new ServerClient(this, identity, connection);

            ClientConnected?.Invoke(this, new ServerClientEventArgs(client));

            lock (_clients)
                _clients.Add(client);

            connection.Listening = true;
            closeConnection = false;
        }
        catch (Exception ex)
        {
            ClientConnectingError?.Invoke(this, new ServerClientConnectExceptionEventArgs(ex));
        }
        finally
        {
            if (closeConnection)
            {
                if (connection != null)
                    try { connection.Close(); }
                    catch { /* ignored */ }

                if (stream != null)
                    try { stream.Close(); }
                    catch { /* ignored */ }

                if (tcpClient != null)
                    try { tcpClient.Close(); }
                    catch { /* ignored */ }
            }
        }
    }

    /// <summary>
    /// Removes client form the client list and invokes <see cref="ClientDisconnected"/> event.
    /// </summary>
    /// <param name="client">Client which raised the event.</param>
    /// <param name="e">Client event argument.</param>
    protected internal virtual void OnClientDisconnected(ServerClient client, NetworkConnectionClosedEventArgs e)
    {
        try
        {
            lock (_clients)
                _clients.Remove(client);

            if (!IsDisposed)
                ClientDisconnected?.Invoke(this, new ServerClientEventArgs(client));
        }
        finally
        {
            client.Dispose();
        }
    }
    /// <summary>
    /// Invokes <see cref="Received"/> event.
    /// </summary>
    /// <param name="client">Client which raised the event.</param>
    /// <param name="e">Client event argument.</param>
    protected internal virtual void OnClientReceived(ServerClient client, NetworkConnectionMessageEventArgs e)
    {
        Received?.Invoke(this, new ServerClientMessageEventArgs(client, e.Message));
    }
    /// <summary>
    /// Invokes <see cref="ReceivedError"/> event.
    /// </summary>
    /// <param name="client">Client which raised the event.</param>
    /// <param name="e">Client event argument.</param>
    protected internal virtual void OnClientReceivedError(ServerClient client, NetworkConnectionMessageErrorEventArgs e)
    {
        ReceivedError?.Invoke(this, new ServerClientMessageErrorEventArgs(client, e.Exception));
    }
    /// <summary>
    /// Invokes <see cref="ControllerResolve"/> event.
    /// </summary>
    /// <param name="client">Client which raised the event.</param>
    /// <param name="e">Client event argument.</param>
    protected internal virtual void OnClientControllerResolve(ServerClient client, NetworkConnectionControllerResolveEventArgs e)
    {
        lock (_controllers)
            if (_controllers.TryGetValue(e.ControllerTypeName, out var controller))
            {
                e.Controller = controller is Func<ServerClient, object> provider
                    ? provider(client)
                    : Activator.CreateInstance((Type)controller);
                return;
            }

        var delegates = ControllerResolve?.GetInvocationList();
        if (delegates != null)
        {
            var e2 = new ServerClientControllerResolveEventArgs(client, e.ControllerTypeName, e.ControllerType);
            foreach (var del in delegates)
            {
                ((EventHandler<ServerClientControllerResolveEventArgs>)del)(this, e2);
                if (e2.Controller != null)
                {
                    e.Controller = e2.Controller;
                    break;
                }
            }
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        DisposeCore(true);
        GC.SuppressFinalize(this);
    }
    private void DisposeCore(bool disposing)
    {
        if (!IsDisposed)
        {
            try
            {
                _listening = false;

                if (_listener != null)
                    try { _listener.Stop(); }
                    catch { /* ignored */ }
                    finally { _listener = null; }

                Dispose(disposing);
            }
            finally
            {
                IsDisposed = true;

                lock (_clients)
                {
                    foreach (var client in _clients)
                    {
                        client.Server = null;

                        if (DisconnectClientsOnDispose)
                            client.Dispose();
                    }

                    _clients.Clear();
                }
            }
        }
    }
    /// <summary>
    /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
    /// </summary>
    protected virtual void Dispose(bool disposing) { }
    private void ThrowIfDisposed()
    {
        if (IsDisposed)
            throw new ObjectDisposedException(null);
    }
}
