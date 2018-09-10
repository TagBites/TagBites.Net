using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;

namespace TagBites.Net
{
    /// <summary>
    /// Provides data for the <see cref="Server"/> events.
    /// </summary>
    public class ServerClientEventArgs : EventArgs
    {
        /// <summary>
        /// Gets client for whom the event was called.
        /// </summary>
        public ServerClient Client { get; }

        internal ServerClientEventArgs(ServerClient client)
        {
            Client = client;
        }
    }

    /// <summary>
    /// Provides data for the <see cref="E:TagBites.Net.Server.ClientConnectingError"/> event.
    /// </summary>
    public class ServerClientConnectExceptionEventArgs : EventArgs
    {
        /// <summary>
        /// Gets a exception.
        /// </summary>
        public Exception Exception { get; }

        internal ServerClientConnectExceptionEventArgs(Exception exception)
        {
            Exception = exception;
        }
    }

    /// <summary>
    /// Provides data for the <see cref="E:TagBites.Net.Server.ClientAuthenticate"/> event.
    /// </summary>
    public class ServerClientAuthenticateEventArgs : EventArgs
    {
        /// <summary>
        /// Gets client credentials.
        /// </summary>
        public ClientCredentials Credentials { get; }
        /// <summary>
        /// Gets or sets value indicating whether authentication procedure has been successful.
        /// </summary>
        public bool Authenticated { get; set; }
        /// <summary>
        /// Gets or sets client identity. It could be assign based on credentials.
        /// </summary>
        public object Identity { get; set; }

        internal ServerClientAuthenticateEventArgs(ClientCredentials credentials)
        {
            Credentials = credentials;
        }
    }

    /// <summary>
    /// Provides data for the <see cref="E:TagBites.Net.Server.Received"/> event.
    /// </summary>
    public class ServerClientMessageEventArgs : ServerClientEventArgs
    {
        /// <summary>
        /// Gets a message.
        /// </summary>
        public object Message { get; }

        internal ServerClientMessageEventArgs(ServerClient client, object message)
            : base(client)
        {
            Message = message;
        }
    }

    /// <summary>
    /// Provides data for the <see cref="E:TagBites.Net.Server.ReceivedError"/> event.
    /// </summary>
    public class ServerClientMessageErrorEventArgs : ServerClientEventArgs
    {
        /// <summary>
        /// Gets a exception.
        /// </summary>
        public Exception Exception { get; }

        internal ServerClientMessageErrorEventArgs(ServerClient client, Exception exception)
            : base(client)
        {
            Exception = exception;
        }
    }

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

        private readonly X509Certificate m_sslCertificate;
        private TcpListener m_listener;
        private bool m_listening;
        private Task m_listeningTask;
        private readonly List<ServerClient> m_clients = new List<ServerClient>();
        private readonly Dictionary<string, Type> m_controllers = new Dictionary<string, Type>();

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
            get => m_listening;
            set
            {
                ThrowIfDisposed();

                if (m_listening != value)
                {
                    m_listening = value;

                    if (m_listening)
                    {
                        m_listeningTask = m_listeningTask != null
                            ? m_listeningTask.ContinueWith(t => ListeningTask())
                            : Task.Run(ListeningTask);
                    }
                    else
                    {
                        try { m_listener?.Stop(); }
                        catch { /* ignored */ }
                    }
                }
            }
        }
        /// <summary>
        /// Gets an address on which server is listening for clients.
        /// </summary>
        public IPEndPoint LocalEndpoint => (IPEndPoint)m_listener.LocalEndpoint;

        /// <summary>
        /// Initializes a new instance of the <see cref="Server"/> class.
        /// </summary>
        /// <param name="host">Address on which server is listening for clients.</param>
        /// <param name="port">Port on which server is listening for clients.</param>
        public Server(string host, int port)
            : this(host, port, null)
        { }
        /// <summary>
        /// Initializes a new instance of the <see cref="Server"/> class.
        /// </summary>
        /// <param name="host">Address on which server is listening for clients.</param>
        /// <param name="port">Port on which server is listening for clients.</param>
        /// <param name="certificate">Certificate used to secure connection (ssl).</param>
        public Server(string host, int port, X509Certificate certificate)
            : this(new IPEndPoint(IPAddress.Parse(host), port), certificate)
        { }
        /// <summary>
        /// Initializes a new instance of the <see cref="Server"/> class.
        /// </summary>
        /// <param name="address">Address on which server is listening for clients.</param>
        public Server(IPEndPoint address)
            : this(address, null)
        { }
        /// <summary>
        /// Initializes a new instance of the <see cref="Server"/> class.
        /// </summary>
        /// <param name="address">Address on which server is listening for clients.</param>
        /// <param name="certificate">Certificate used to secure connection (ssl).</param>
        public Server(IPEndPoint address, X509Certificate certificate)
        {
            if (address == null)
                throw new ArgumentNullException(nameof(address));

            m_listener = new TcpListener(address);
            m_sslCertificate = certificate;
        }
        /// <inheritdoc />
        ~Server()
        {
            DisposeCore(false);
        }


        /// <summary>
        /// Register new local controller.
        /// </summary>
        /// <typeparam name="T">Type of controller.</typeparam>
        public void Use<T>() where T : new()
        {
            var controllerType = typeof(T);
            var name = controllerType.FullName + ", " + controllerType.Assembly.GetName().Name;

            lock (m_controllers)
                m_controllers[name] = typeof(T);
        }

        /// <summary>
        /// Returns connected clients.
        /// </summary>
        public ServerClient[] GetClients()
        {
            lock (m_clients)
                return m_clients.ToArray();
        }
        /// <summary>
        /// Returns connected client with the given <paramref name="identity"/>.
        /// </summary>
        /// <param name="identity">Client identity.</param>
        public ServerClient GetClient(object identity)
        {
            if (identity == null)
                throw new ArgumentNullException(nameof(identity));

            lock (m_clients)
                return m_clients.FirstOrDefault(x => x.Identity != null && x.Identity.Equals(identity));
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
                throw new AggregateException("Error occured while sending message to all clients.", ex);
        }

        private async void ListeningTask()
        {
            m_listener.Start();

            while (Listening)
            {
                try
                {
                    var tcpClient = await m_listener.AcceptTcpClientAsync();

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

            try { m_listener?.Stop(); }
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
                if (m_sslCertificate == null)
                {
                    var networkStream = tcpClient.GetStream();
                    //networkStream.ReadTimeout = ReceiveTimeOut;
                    //networkStream.WriteTimeout = SendTimeOut;

                    stream = networkStream;
                    connection = new NetworkConnection(tcpClient, networkStream);
                }
                else
                {
                    var sslStream = new SslStream(tcpClient.GetStream(), false);
                    //sslStream.ReadTimeout = ReceiveTimeOut;
                    //sslStream.WriteTimeout = SendTimeOut;
                    sslStream.AuthenticateAsServer(m_sslCertificate, false, SslProtocols.Tls, true);

                    stream = sslStream;
                    connection = new NetworkConnection(tcpClient, sslStream);
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
                connection.StartEvaluation();

                // Create
                var client = new ServerClient(this, identity, connection);

                ClientConnected?.Invoke(this, new ServerClientEventArgs(client));

                lock (m_clients)
                    m_clients.Add(client);

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
                lock (m_clients)
                    m_clients.Remove(client);

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
            lock (m_controllers)
                if (m_controllers.TryGetValue(e.ControllerTypeName, out var type))
                {
                    e.Controller = Activator.CreateInstance(type);
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
                    m_listening = false;

                    if (m_listener != null)
                        try { m_listener.Stop(); }
                        catch { /* ignored */ }
                        finally { m_listener = null; }

                    Dispose(disposing);
                }
                finally
                {
                    IsDisposed = true;

                    lock (m_clients)
                    {
                        foreach (var client in m_clients)
                        {
                            client.Server = null;

                            if (DisconnectClientsOnDispose)
                                client.Dispose();
                        }

                        m_clients.Clear();
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
}
