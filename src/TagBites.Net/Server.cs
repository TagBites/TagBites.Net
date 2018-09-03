using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;

namespace TagBites.Net
{
    public class ServerClientEventArgs : EventArgs
    {
        public ServerClient Client { get; }

        internal ServerClientEventArgs(ServerClient client)
        {
            Client = client;
        }
    }
    public class ServerClientConnectExceptionEventArgs : EventArgs
    {
        public Exception Exception { get; }

        internal ServerClientConnectExceptionEventArgs(Exception exception)
        {
            Exception = exception;
        }
    }
    public class ServerClientAuthenticateEventArgs : EventArgs
    {
        public ClientCredentials Credentials { get; }
        public bool Authenticated { get; set; }
        public object Identity { get; set; }

        internal ServerClientAuthenticateEventArgs(ClientCredentials credentials)
        {
            Credentials = credentials;
        }
    }
    public class ServerClientControllerResolveEventArgs : ServerClientEventArgs
    {
        public string ControllerTypeName { get; }
        public Type ControllerType { get; }
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
        public event EventHandler<ServerClientAuthenticateEventArgs> ClientAuthenticate;
        public event EventHandler<ServerClientConnectExceptionEventArgs> ClientConnectingError;
        public event EventHandler<ServerClientEventArgs> ClientConnected;
        public event EventHandler<ServerClientEventArgs> ClientDisconnected;
        public event EventHandler<ServerClientControllerResolveEventArgs> ControllerResolve;

        private readonly X509Certificate m_sslCertificate;
        private TcpListener m_listener;
        private bool m_listening;
        private Task m_listeningTask;
        private readonly List<ServerClient> m_clients = new List<ServerClient>();

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
                            : Task.Run((Action)ListeningTask);
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
        /// <param name="identity"></param>
        /// <returns></returns>
        public ServerClient GetClient(object identity)
        {
            if (identity == null)
                throw new ArgumentNullException(nameof(identity));

            lock (m_clients)
                return m_clients.FirstOrDefault(x => x.Identity != null && x.Identity.Equals(identity));
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
                client.ControllerResolve += Client_ControllerResolve;
                client.Disconnected += Client_Disconnected;

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

        private void Client_ControllerResolve(object sender, NetworkConnectionControllerResolveEventArgs e)
        {
            var delegates = ControllerResolve?.GetInvocationList();
            if (delegates != null)
            {
                var e2 = new ServerClientControllerResolveEventArgs((ServerClient)sender, e.ControllerTypeName, e.ControllerType);
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
        private void Client_Disconnected(object sender, NetworkConnectionClosedEventArgs e)
        {
            var client = (ServerClient)sender;
            try
            {
                lock (m_clients)
                    m_clients.Remove((ServerClient)sender);

                if (!IsDisposed)
                    ClientDisconnected?.Invoke(this, new ServerClientEventArgs(client));
            }
            finally
            {
                client.Dispose();
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
                            client.Disconnected -= Client_Disconnected;
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
