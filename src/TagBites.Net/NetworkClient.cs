using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TagBites.Net
{
    /// <summary>
    /// TCP client which allows to send objects messages and execute remote methods.
    /// This class is thread safe.
    /// </summary>
    public abstract class NetworkClient : IDisposable
    {
        /// <summary>
        /// 
        /// </summary>
        public event EventHandler<NetworkConnectionClosedEventArgs> Disconnected;
        /// <summary>
        /// 
        /// </summary>
        public event EventHandler<NetworkConnectionMessageEventArgs> Received;
        /// <summary>
        /// 
        /// </summary>
        public event EventHandler<NetworkConnectionMessageErrorEventArgs> ReceivedError;
        /// <summary>
        /// 
        /// </summary>
        public event EventHandler<NetworkConnectionControllerResolveEventArgs> ControllerResolve;

        private NetworkConnection _connection;

        /// <summary>
        /// Gets connection instance.
        /// </summary>
        protected NetworkConnection Connection
        {
            get => _connection;
            set
            {
                if (_connection != value)
                {
                    ThrowIfDisposed();

                    if (_connection != null)
                    {
                        Connection.Closed -= OnConnectionClosed;
                        Connection.Received -= OnReceived;
                        Connection.ReceivedError -= OnReceivedError;
                        Connection.ControllerResolve -= OnControllerResolve;
                    }

                    _connection = value;

                    if (_connection != null)
                    {
                        Connection.Closed += OnConnectionClosed;
                        Connection.Received += OnReceived;
                        Connection.ReceivedError += OnReceivedError;
                        Connection.ControllerResolve += OnControllerResolve;
                    }
                }
            }
        }

        /// <summary>
        /// Gets a value indicating whether object already disposed or not.
        /// </summary>
        public bool IsDisposed { get; private set; }
        /// <summary>
        /// Gets a value indicating whether connection is active.
        /// </summary>
        public bool IsConnected => !IsDisposed && Connection?.IsConnected == true;

        /// <inheritdoc />
        ~NetworkClient()
        {
            DisposeCore(false);
        }


        /// <summary>
        /// Returns the remote controller.
        /// </summary>
        /// <typeparam name="T">Type of controller.</typeparam>
        /// <returns>Remote controller.</returns>
        public T GetController<T>()
        {
            ThrowIfDisconnected();
            return Connection.GetController<T>();
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="message"></param>
        /// <returns></returns>
        public async Task SendAsync(object message)
        {
            ThrowIfDisconnected();
            await Connection.WriteAsync(message).ConfigureAwait(false);
        }

        /// <summary>
        /// Invokes <see cref="Disconnected"/> event.
        /// </summary>
        protected virtual void OnConnectionClosed(object sender, NetworkConnectionClosedEventArgs e) => Disconnected?.Invoke(this, e);
        /// <summary>
        /// Invokes <see cref="Received"/> event.
        /// </summary>
        protected virtual void OnReceived(object sender, NetworkConnectionMessageEventArgs e) => Received?.Invoke(this, e);
        /// <summary>
        /// Invokes <see cref="ReceivedError"/> event.
        /// </summary>
        protected virtual void OnReceivedError(object sender, NetworkConnectionMessageErrorEventArgs e) => ReceivedError?.Invoke(this, e);
        /// <summary>
        /// Invokes <see cref="ControllerResolve"/> event.
        /// </summary>
        protected virtual void OnControllerResolve(object sender, NetworkConnectionControllerResolveEventArgs e) => ControllerResolve?.Invoke(this, e);

        /// <summary>
        /// Closes the connection.
        /// </summary>
        public void Close()
        {
            Connection?.Close();
            Connection = null;
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
                    Dispose(disposing);

                    if (Connection != null)
                        try { Connection.Dispose(); }
                        catch { /* ignored */ }
                        finally { Connection = null; }
                }
                finally
                {
                    IsDisposed = true;
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
        private void ThrowIfDisconnected()
        {
            if (!IsConnected)
                throw new InvalidOperationException("Client is not connected.");
        }
    }
}
