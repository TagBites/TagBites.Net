using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;

namespace TagBites.Net
{
    /// <summary>
    /// TCP client which allows to send objects messages and execute remote methods.
    /// This class is thread safe.
    /// </summary>
    public class Client : NetworkClient
    {
        /// <summary>
        /// Occurs after connection is established to the server and client is authenticated.
        /// </summary>
        public event EventHandler Connected;

        private readonly ClientCredentials m_credentials;
        private readonly Dictionary<string, Type> m_controllers = new Dictionary<string, Type>();

        /// <summary>
        /// Gets the remote endpoint.
        /// </summary>
        public EndPoint RemoteEndPoint { get; private set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="Client"/> class.
        /// </summary>
        /// <param name="host">Address on which server is listening for clients.</param>
        /// <param name="port">Port on which server is listening for clients.</param>
        public Client(string host, int port)
            : this(host, port, null)
        { }
        /// <summary>
        /// Initializes a new instance of the <see cref="Client"/> class.
        /// </summary>
        /// <param name="host">Address on which server is listening for clients.</param>
        /// <param name="port">Port on which server is listening for clients.</param>
        /// <param name="credentials">Client credentials used for authentication.</param>
        public Client(string host, int port, ClientCredentials credentials)
        {
            if (host == null)
                throw new ArgumentNullException(nameof(host));
            if (port < 0 || port > ushort.MaxValue)
                throw new ArgumentOutOfRangeException(nameof(port));

            RemoteEndPoint = new IPEndPoint(IPAddress.Parse(host), port);
            m_credentials = credentials;
        }
        /// <summary>
        /// Initializes a new instance of the <see cref="Client"/> class.
        /// </summary>
        /// <param name="address">Address on which server is listening for clients.</param>
        public Client(IPEndPoint address)
            : this(address, null)
        { }
        /// <summary>
        /// Initializes a new instance of the <see cref="Client"/> class.
        /// </summary>
        /// <param name="address">Address on which server is listening for clients.</param>
        /// <param name="credentials">Client credentials used for authentication.</param>
        public Client(IPEndPoint address, ClientCredentials credentials)
        {
            RemoteEndPoint = address ?? throw new ArgumentNullException(nameof(address));
            m_credentials = credentials;
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
        /// Starts the connection.
        /// </summary>
        public Task ConnectAsync() => ConnectAsync(false);
        /// <summary>
        /// Starts the SSL connection .
        /// </summary>
        public Task ConnectSslAsync() => ConnectAsync(true);
        /// <summary>
        /// Starts the SSL connection.
        /// </summary>
        /// <param name="serverName">Name of the server (used for certificate validation).</param>
        public Task ConnectSslAsync(string serverName) => ConnectAsync(true, serverName);

        private Task ConnectAsync(bool useSsl, string serverName = null)
        {
            return Task.Run(async () => await ConnectAsyncCore(useSsl, serverName));
        }
        private async Task ConnectAsyncCore(bool useSsl, string serverName = null)
        {
            if (IsConnected)
                throw new InvalidOperationException();

            TcpClient client = null;
            Stream stream = null;
            NetworkConnection connection = null;
            var closeConnection = true;

            try
            {
                try
                {
                    client = new TcpClient((IPEndPoint)RemoteEndPoint);

                    if (useSsl)
                    {
                        var sslStream = new SslStream(client.GetStream(), false, OnValidateServerCertificate, null);
                        sslStream.AuthenticateAsClient(serverName ?? String.Empty);

                        stream = sslStream;
                        connection = new NetworkConnection(client, sslStream);
                    }
                    else
                    {
                        stream = client.GetStream();
                        connection = new NetworkConnection(client, (NetworkStream)stream);
                    }
                }
                catch (Exception e)
                {
                    throw new NetworkConnectionOpenException(e);
                }

                // Authenticate
                try
                {
                    await connection.WriteAsync(m_credentials);
                    var result = await connection.ReadAsync();

                    if (!Equals(result, true))
                        throw new ClientAuthenticationException();
                }
                catch (Exception e)
                {
                    throw new ClientAuthenticationException(e);
                }

                Connection = connection;
                connection.StartEvaluation();

                OnConnected();

                RemoteEndPoint = connection.TcpClient.Client.RemoteEndPoint;
                connection.Listening = true;
                closeConnection = false;
            }
            finally
            {
                if (closeConnection || IsDisposed)
                {
                    Connection = null;

                    if (stream != null)
                        try { stream.Dispose(); }
                        catch { /* ignored */ }

                    if (client != null)
                        try { ((IDisposable)client).Dispose(); }
                        catch { /* ignored */ }

                    if (connection != null)
                        try { connection.Dispose(); }
                        catch { /* ignored */ }
                }
            }
        }

        /// <summary>
        /// Validates server certificate.
        /// </summary>
        protected virtual bool OnValidateServerCertificate(object sender, X509Certificate certificate, X509Chain chain, SslPolicyErrors sslPolicyErrors)
        {
            if (sslPolicyErrors == SslPolicyErrors.None)
                return true;

            return false;
        }
        /// <summary>
        /// Invokes <see cref="Connected"/> event.
        /// </summary>
        protected virtual void OnConnected()
        {
            Connected?.Invoke(this, new EventArgs());
        }

        /// <inheritdoc />
        protected override void OnControllerResolve(object sender, NetworkConnectionControllerResolveEventArgs e)
        {
            lock (m_controllers)
                if (m_controllers.TryGetValue(e.ControllerTypeName, out var type))
                {
                    e.Controller = Activator.CreateInstance(type);
                    return;
                }

            base.OnControllerResolve(sender, e);
        }
    }
}
