using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
        public event EventHandler Connected;

        private readonly ClientCredentials m_credentials;

        /// <summary>
        /// 
        /// </summary>
        public string Host { get; }
        /// <summary>
        /// 
        /// </summary>
        public int Port { get; }

        public Client(string host, int port)
            : this(host, port, null)
        { }
        public Client(string host, int port, ClientCredentials credentials)
        {
            if (host == null)
                throw new ArgumentNullException(nameof(host));
            if (port < 0 || port > ushort.MaxValue)
                throw new ArgumentOutOfRangeException(nameof(port));

            Host = host;
            Port = port;
            m_credentials = credentials;
        }


        public Task ConnectAsync() => ConnectAsync(false);
        public Task ConnectSslAsync() => ConnectAsync(true);
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
                    client = new TcpClient(Host, Port);

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

        protected virtual bool OnValidateServerCertificate(object sender, X509Certificate certificate, X509Chain chain, SslPolicyErrors sslPolicyErrors)
        {
            if (sslPolicyErrors == SslPolicyErrors.None)
                return true;

            return false;
        }
        protected virtual void OnConnected()
        {
            Connected?.Invoke(this, new EventArgs());
        }
    }
}
