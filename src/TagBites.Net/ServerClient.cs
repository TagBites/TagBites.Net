using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace TagBites.Net
{
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

        internal ServerClient(Server server, object identity, NetworkConnection connection)
        {
            Identity = identity;
            Server = server;
            Connection = connection;
        }
    }
}
