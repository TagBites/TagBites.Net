using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Net.Security;
using System.Net.Sockets;
using System.Reflection;
using System.Runtime.Serialization;
using System.Text;

namespace TagBites.Net
{
    /// <summary>
    /// Provides data for the <see cref="E:TagBites.Net.NetworkConnection.Closed"/> event.
    /// </summary>
    public class NetworkConnectionClosedEventArgs : EventArgs
    {
        /// <summary>
        /// Gets a exception. Returns <c>null</c> when connection has been closed normally.
        /// </summary>
        public Exception Exception { get; }

        internal NetworkConnectionClosedEventArgs(Exception exception)
        {
            Exception = exception;
        }
    }

    /// <summary>
    /// Provides data for the <see cref="E:TagBites.Net.NetworkConnection.Received"/> event.
    /// </summary>
    public class NetworkConnectionMessageEventArgs : EventArgs
    {
        /// <summary>
        /// Gets a message.
        /// </summary>
        public object Message { get; }

        internal NetworkConnectionMessageEventArgs(object message)
        {
            Message = message;
        }
    }

    /// <summary>
    /// Provides data for the <see cref="E:TagBites.Net.NetworkConnection.ReceivedError"/> event.
    /// </summary>
    public class NetworkConnectionMessageErrorEventArgs : EventArgs
    {
        /// <summary>
        /// Gets a exception.
        /// </summary>
        public Exception Exception { get; }

        internal NetworkConnectionMessageErrorEventArgs(Exception exception)
        {
            Exception = exception;
        }
    }

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

    /// <summary>
    /// TCP connection which allows to send objects messages and execute remote methods.
    /// </summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public sealed class NetworkConnection : IDisposable
    {
        /// <summary>
        /// Occurs when connection closes.
        /// </summary>
        public event EventHandler<NetworkConnectionClosedEventArgs> Closed;
        /// <summary>
        /// Occurs when receives message.
        /// </summary>
        public event EventHandler<NetworkConnectionMessageEventArgs> Received;
        /// <summary>
        /// Occurs when it was unable to receive message (eg. deserialization error).
        /// </summary>
        public event EventHandler<NetworkConnectionMessageErrorEventArgs> ReceivedError;
        /// <summary>
        /// Occurs when endpoint requests access to controller for the first time.
        /// </summary>
        public event EventHandler<NetworkConnectionControllerResolveEventArgs> ControllerResolve;

        private readonly object m_disposeLocker = new object();
        private SemaphoreSlim m_readSemaphore = new SemaphoreSlim(1);
        private SemaphoreSlim m_writeSemaphore = new SemaphoreSlim(1);
        private readonly CancellationToken m_token;
        private CancellationTokenSource m_tokenSource;

        private bool m_listening;
        private Task m_listeningTask;

        private int m_messageId;
        private readonly List<ControllerMethodInvokeState> m_messages = new List<ControllerMethodInvokeState>();
        private readonly List<object> m_remoteControllers = new List<object>();
        private readonly Dictionary<string, object> m_controllers = new Dictionary<string, object>();
        private readonly NetworkConfig _config;

        internal TcpClient TcpClient { get; private set; }
        private Stream Stream { get; set; }

        /// <summary>
        /// Gets a value indicating whether object already disposed or not.
        /// </summary>
        public bool IsDisposed { get; private set; }
        /// <summary>
        /// Gets a value indicating whether connection is active.
        /// </summary>
        public bool IsConnected => !IsDisposed && TcpClient != null && TcpClient.Connected;
        /// <summary>
        /// Gets or sets a value indicating whether is listening for commands/messages.
        /// Setting <c>true</c> starts background thread.
        /// </summary>
        public bool Listening
        {
            get => m_listening && !IsDisposed;
            set
            {
                if (IsDisposed)
                    throw new ObjectDisposedException(null);

                if (m_listening != value)
                {
                    if (value)
                    {
                        m_listeningTask = m_listeningTask != null
                            ? m_listeningTask.ContinueWith(t => ListeningTask())
                            : Task.Run(ListeningTask);
                    }

                    m_listening = value;
                }
            }
        }

        internal NetworkConnection(NetworkConfig config, TcpClient tcpClient, NetworkStream networkStream)
            : this(config, tcpClient, (Stream)networkStream)
        { }
        internal NetworkConnection(NetworkConfig config, TcpClient tcpClient, SslStream sslStream)
            : this(config, tcpClient, (Stream)sslStream)
        { }
        private NetworkConnection(NetworkConfig config, TcpClient tcpClient, Stream stream)
        {
            if (config == null)
                throw new ArgumentNullException(nameof(config));
            if (tcpClient == null)
                throw new ArgumentNullException(nameof(tcpClient));
            if (stream == null)
                throw new ArgumentNullException(nameof(stream));

            if (!tcpClient.Connected)
                throw new ArgumentException("TcpClient is not connected.", nameof(tcpClient));

            TcpClient = tcpClient;
            tcpClient.ReceiveTimeout = Timeout.Infinite;
            tcpClient.SendTimeout = Timeout.Infinite;
            Stream = stream;
            _config = config;

            m_tokenSource = new CancellationTokenSource();
            m_token = m_tokenSource.Token;
        }
        /// <inheritdoc />
        ~NetworkConnection()
        {
            lock (m_disposeLocker)
                Dispose(null, false);
        }


        private async void ListeningTask()
        {
            while (Listening)
            {
                try
                {
                    var message = await ReadAsyncCore().ConfigureAwait(false);
                    Received?.Invoke(this, new NetworkConnectionMessageEventArgs(message));
                }
                catch (NetworkConnectionBreakException)
                {
                    return;
                }
                catch (Exception e)
                {
                    if (IsDisposed)
                        return;

                    ReceivedError?.Invoke(this, new NetworkConnectionMessageErrorEventArgs(e));
                }
            }
        }

        #region Controllers

        /// <summary>
        /// Returns the remote controller.
        /// </summary>
        /// <typeparam name="T">Type of controller.</typeparam>
        /// <returns>Remote controller.</returns>
        public T GetController<T>()
        {
            ThrowIfDisposed();

            lock (m_remoteControllers)
            {
                var controller = m_remoteControllers.FirstOrDefault(x => x is T);
                if (controller == null)
                {
                    controller = ControllerProxy.Create<T>(this);
                    m_remoteControllers.Add(controller);
                }

                return (T)controller;
            }
        }

        /// <summary>
        /// Register local controller.
        /// </summary>
        /// <typeparam name="TControllerInterface">Controller interface.</typeparam>
        /// <typeparam name="TController">Controller type.</typeparam>
        public void Use<TControllerInterface, TController>() where TController : TControllerInterface, new()
        {
            Use<TControllerInterface, TController>(new TController());
        }
        /// <summary>
        /// Register local controller.
        /// </summary>
        /// <typeparam name="TControllerInterface">Controller interface.</typeparam>
        /// <typeparam name="TController">Controller type.</typeparam>
        /// <param name="controller">Controller instance.</param>
        public void Use<TControllerInterface, TController>(TController controller) where TController : TControllerInterface
        {
            if (controller == null)
                throw new ArgumentNullException(nameof(controller));

            var controllerType = typeof(TControllerInterface);
            var name = controllerType.FullName + ", " + controllerType.Assembly.GetName().Name;

            lock (m_controllers)
                m_controllers[name] = controller;
        }

        private async void OnTrackMessage(TrackMessage message)
        {
            if (message.InResponseToId != 0)
                OnTrackMessageResponse(message);
            else
            {
                var response = new TrackMessage()
                {
                    MessageId = Interlocked.Increment(ref m_messageId),
                    InResponseToId = message.MessageId
                };

                if (message.Value is ControllerMethodInvokeModel invokeModel)
                {
                    response.Value = await RemoteControllerMethodInvoke(message.MessageId, invokeModel);
                    await WriteAsync(response);
                }
            }
        }
        private void OnTrackMessageResponse(TrackMessage message)
        {
            lock (m_messages)
            {
                for (var i = 0; i < m_messages.Count; i++)
                {
                    var msg = m_messages[i];
                    if (msg.MessageId == message.InResponseToId)
                    {
                        m_messages.RemoveAt(i);

                        if (message.Value is ControllerMethodInvokeResultModel rm)
                        {
                            if (rm.ExceptionCode > 0)
                                msg.Exception = new NetworkControllerInvocationException((NetworkControllerInvocationExceptionType)rm.ExceptionCode, rm.ExceptionMessage) { RemoteException = rm.FullException };
                            else
                                msg.Result = rm.Result;
                        }

                        msg.Semaphore.Release();

                        break;
                    }
                }
            }
        }
        private void OnTrackMessageResponseError(int inResponseToId, Exception error)
        {
            lock (m_messages)
            {
                for (var i = 0; i < m_messages.Count; i++)
                {
                    var msg = m_messages[i];
                    if (msg.MessageId == inResponseToId)
                    {
                        m_messages.RemoveAt(i);

                        msg.Exception = new NetworkControllerInvocationException(NetworkControllerInvocationExceptionType.DataReceivingError, null, error);
                        msg.Semaphore.Release();

                        break;
                    }
                }
            }
        }

        private async Task<object> ControllerMethodInvoke(Type controllerType, MethodInfo method, object[] parameters)
        {
            var model = new ControllerMethodInvokeModel()
            {
                ControllerTypeFullName = controllerType.FullName + ", " + controllerType.Assembly.GetName().Name,
                MethodName = method.Name,
                ParametersTypesFullNames = method.GetParameters().Select(x => x.ParameterType.FullName).ToArray(),
                Parameters = parameters
            };
            var message = new TrackMessage()
            {
                MessageId = Interlocked.Increment(ref m_messageId),
                Value = model
            };
            var info = new ControllerMethodInvokeState()
            {
                MessageId = message.MessageId,
                Semaphore = new SemaphoreSlim(0)
            };
            lock (m_messages)
                m_messages.Add(info);

            await WriteAsync(message).ConfigureAwait(false);
            await info.Semaphore.WaitAsync(m_token).ConfigureAwait(false);

            if (info.Exception != null)
                throw info.Exception;

            return info.Result;
        }
        private async Task<ControllerMethodInvokeResultModel> RemoteControllerMethodInvoke(int messageId, ControllerMethodInvokeModel invokeModel)
        {
            var result = new ControllerMethodInvokeResultModel();

            // Find controller
            object controller;
            lock (m_controllers)
            {
                var controllerName = invokeModel.ControllerTypeFullName;

                if (!m_controllers.TryGetValue(controllerName, out controller))
                {
                    try
                    {
                        var delegates = ControllerResolve?.GetInvocationList();
                        if (delegates != null)
                        {
                            // Type
                            Type controllerType = null;

                            try { controllerType = Type.GetType(controllerName); }
                            catch { /* ignored */ }

                            // Resolve
                            var e = new NetworkConnectionControllerResolveEventArgs(controllerName, controllerType);

                            foreach (var del in delegates)
                            {
                                ((EventHandler<NetworkConnectionControllerResolveEventArgs>)del)(this, e);
                                if (e.Controller != null)
                                {
                                    controller = e.Controller;
                                    m_controllers[controllerName] = controller;
                                    break;
                                }
                            }
                        }
                    }
                    catch (Exception e)
                    {
                        var ex = e;
                        if (ex is TargetInvocationException { InnerException: not null } ie)
                            ex = ie.InnerException;

                        result.ExceptionMessage = ex.Message;
                        result.FullException = ex.ToString();
                    }
                }
            }

            if (controller == null)
            {
                result.ExceptionCode = (int)NetworkControllerInvocationExceptionType.ControllerNotFound;
                return result;
            }

            // Find method
            var method = controller.GetType().GetMethods().Where(x =>
                {
                    if (x.Name != invokeModel.MethodName)
                        return false;

                    var parameters = x.GetParameters();
                    if (parameters.Length != invokeModel.ParametersTypesFullNames.Length || parameters.Length != invokeModel.Parameters.Length)
                        return false;

                    for (var i = 0; i < parameters.Length; i++)
                    {
                        if (parameters[i].ParameterType.FullName != invokeModel.ParametersTypesFullNames[i])
                            return false;
                    }

                    return true;
                })
                .FirstOrDefault();

            if (method == null)
            {
                result.ExceptionCode = (int)NetworkControllerInvocationExceptionType.MethodNotFound;
                return result;
            }

            // Execute
            try
            {
                var value = method.Invoke(controller, invokeModel.Parameters);
                if (value is Task t)
                {
                    await Task.WhenAll(t).ConfigureAwait(false);
                    value = t.GetType().GetProperty("Result")?.GetValue(t);

                    if (value?.GetType().FullName == "System.Threading.Tasks.VoidTaskResult")
                        value = null;
                }

                result.Result = value;
            }
            catch (Exception e)
            {
                var ex = e;
                if (ex is TargetInvocationException { InnerException: not null } ie)
                    ex = ie.InnerException;

                result.ExceptionCode = (int)NetworkControllerInvocationExceptionType.MethodInvokeException;
                result.ExceptionMessage = ex.Message;
                result.FullException = ex.ToString();
            }

            return result;
        }

        #endregion

        #region Network read/write

        /// <summary>
        /// Reads next object from socket. Thread waits until object is available.
        /// </summary>
        public Task<object> ReadAsync()
        {
            if (Listening)
                throw new InvalidOperationException("Can not read while listening is enabled.");

            return ReadAsyncCore();
        }
        private async Task<object> ReadAsyncCore()
        {
            // ReSharper disable once InconsistentlySynchronizedField
            try
            {
                await m_readSemaphore.WaitAsync(m_token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                if (IsDisposed)
                    throw new NetworkConnectionBreakException(null);

                throw;
            }


            try
            {
                while (true)
                {
                    try
                    {
                        var result = await ReadObjectAsyncCore().ConfigureAwait(false);
                        if (result is TrackMessage m)
                        {
#pragma warning disable 4014
                            Task.Run(() => OnTrackMessage(m), m_token);
#pragma warning restore 4014
                            continue;
                        }

                        return result;
                    }
                    catch (NetworkSerializationException e)
                    {
                        if (e.InResponseToId != 0)
                        {
#pragma warning disable 4014
                            Task.Run(() => OnTrackMessageResponseError(e.InResponseToId, e), m_token);
#pragma warning restore 4014
                            continue;
                        }
                        else if (e.MessageId != 0)
                        {
#pragma warning disable 4014
                            Task.Run(async () =>
                            {
                                var trackMessage = new TrackMessage()
                                {
                                    InResponseToId = e.MessageId,
                                    Value = new ControllerMethodInvokeResultModel()
                                    {
                                        ExceptionCode = (int)NetworkControllerInvocationExceptionType.DataReceivingError,
                                        ExceptionMessage = e.Message,
                                        FullException = e.ToString()
                                    }
                                };
                                await WriteAsync(trackMessage);
                            }, m_token);
#pragma warning restore 4014
                        }

                        throw;
                    }
                    catch (NetworkConnectionBreakException e)
                    {
                        lock (m_disposeLocker)
                        {
                            if (!IsDisposed)
                                Dispose(e);

                            throw;
                        }
                    }
                    catch (Exception e)
                    {
                        lock (m_disposeLocker)
                        {
                            if (IsDisposed)
                                throw new NetworkConnectionBreakException(null);

                            var ex = new NetworkObjectProtocolViolationException(e);
                            Dispose(ex);
                            throw ex;
                        }
                    }
                }
            }
            finally
            {
                try
                {
                    // ReSharper disable once InconsistentlySynchronizedField
                    m_readSemaphore?.Release();
                }
                catch { /* ignored (disposed) */ }
            }
        }

        /// <summary>
        /// Writes object to socket.
        /// </summary>
        /// <param name="value">Object to be written on the connection.</param>
        public async Task WriteAsync(object value)
        {
            // ReSharper disable once InconsistentlySynchronizedField
            try
            {
                await m_writeSemaphore.WaitAsync(m_token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                if (IsDisposed)
                    throw new NetworkConnectionBreakException(null);

                throw;
            }

            try
            {
                try
                {
                    await WriteObjectAsyncCore(value).ConfigureAwait(false);
                }
                catch (NetworkSerializationException)
                {
                    throw;
                }
                catch (NetworkConnectionBreakException e)
                {
                    lock (m_disposeLocker)
                    {
                        if (!IsDisposed)
                            Dispose(e);

                        throw;
                    }
                }
                catch (Exception e)
                {
                    lock (m_disposeLocker)
                    {
                        if (IsDisposed)
                            throw new NetworkConnectionBreakException(null);

                        var ex = new NetworkObjectProtocolViolationException(e);
                        Dispose(ex);
                        throw ex;
                    }
                }
            }
            finally
            {
                try
                {
                    // ReSharper disable once InconsistentlySynchronizedField
                    m_writeSemaphore?.Release();
                }
                catch { /* ignored (disposed) */ }
            }
        }

        #endregion

        #region Network low level read/write

        private async Task WriteObjectAsyncCore(object value)
        {
            var listBuffer = new List<byte>();

            // Header
            var trackMessage = value as TrackMessage;
            var messageId = trackMessage?.MessageId ?? 0;
            var inResponseToId = trackMessage?.InResponseToId ?? 0;

            if (trackMessage != null)
                value = trackMessage.Value;

            var typeCode = Convert.GetTypeCode(value);

            listBuffer.AddRange(BitConverter.GetBytes(messageId));
            listBuffer.AddRange(BitConverter.GetBytes(inResponseToId));
            listBuffer.Add((byte)typeCode);

            if (typeCode != TypeCode.Empty && typeCode != TypeCode.DBNull)
            {
                // Encoding
                var encoding = _config.Encoding;
                listBuffer.AddRange(BitConverter.GetBytes(encoding.CodePage));

                // Value
                byte[] content;
                switch (typeCode)
                {
                    case TypeCode.String:
                        content = encoding.GetBytes((string)value);
                        break;

                    case TypeCode.Object:
                        {
                            var valueType = value.GetType();
                            string typeName;

                            if (valueType == typeof(byte[]))
                                typeName = "byte[]";
                            else
                                typeName = $"{valueType.FullName}, {valueType.Assembly.GetName().Name}";

                            // ReSharper disable once AssignNullToNotNullAttribute
                            var type = encoding.GetBytes(typeName);
                            listBuffer.AddRange(BitConverter.GetBytes(type.Length));
                            listBuffer.AddRange(type);

                            try
                            {
                                if (valueType == typeof(byte[]))
                                    content = (byte[])value;
                                else
                                {
                                    using (var ms = new MemoryStream())
                                    {
                                        _config.Serializer.Serialize(ms, value);
                                        content = ms.ToArray();
                                    }
                                }
                            }
                            catch (Exception ex)
                            {
                                throw new NetworkSerializationException(valueType.AssemblyQualifiedName, ex, 0, inResponseToId);
                            }
                        }
                        break;

                    case TypeCode.DateTime:
                        content = encoding.GetBytes(((DateTime)value).ToString("o", CultureInfo.InvariantCulture));
                        break;

                    default:
                        // ReSharper disable once AssignNullToNotNullAttribute
                        content = encoding.GetBytes(Convert.ToString(value, CultureInfo.InvariantCulture));
                        break;
                }

                listBuffer.AddRange(BitConverter.GetBytes(content.Length));
                listBuffer.AddRange(content);
            }

            // Write
            await WriteAsync(listBuffer.ToArray()).ConfigureAwait(false);
        }
        private async Task<object> ReadObjectAsyncCore()
        {
            var buffer = new byte[12];

            // Header
            await ReadAsync(buffer, 9).ConfigureAwait(false);

            var messageId = BitConverter.ToInt32(buffer, 0);
            var inResponseToId = BitConverter.ToInt32(buffer, 4);
            var typeCode = (TypeCode)buffer[8];

            if (typeCode == TypeCode.Empty || typeCode == TypeCode.DBNull)
            {
                return messageId == 0 && inResponseToId == 0
                    ? null
                    : new TrackMessage() { MessageId = messageId, InResponseToId = inResponseToId };
            }

            // Encoding
            await ReadAsync(buffer, 4).ConfigureAwait(false);
            var encodingCodePage = BitConverter.ToInt32(buffer, 0);

            var encoding = _config.Encoding;
            if (encoding.CodePage != encodingCodePage)
                encoding = Encoding.GetEncoding(encodingCodePage);

            // Type name length
            string typeName = null;
            Type valueType = null;

            if (typeCode == TypeCode.Object)
            {
                await ReadAsync(buffer, 4).ConfigureAwait(false);
                var typeNameLength = BitConverter.ToInt32(buffer, 0);

                var typeNameBuffer = new byte[typeNameLength];
                await ReadAsync(typeNameBuffer).ConfigureAwait(false);

                typeName = encoding.GetString(typeNameBuffer);

                if (typeName == "byte[]")
                    valueType = typeof(byte[]);
                else
                    valueType = Type.GetType(typeName);
            }

            // Length
            await ReadAsync(buffer, 4).ConfigureAwait(false);
            var contentLength = BitConverter.ToInt32(buffer, 0);

            // Content
            const int packLength = 2048;
            var content = new byte[contentLength];

            for (var i = 0; i < contentLength;)
            {
                var left = contentLength - i;
                var toRead = packLength < left
                    ? packLength
                    : left;

                await ReadAsync(content, i, toRead).ConfigureAwait(false);
                i += toRead;
            }

            // Convert
            object value;

            switch (typeCode)
            {
                case TypeCode.DateTime:
                    value = DateTime.Parse(encoding.GetString(content), CultureInfo.InvariantCulture);
                    break;

                case TypeCode.String:
                    value = encoding.GetString(content);
                    break;

                case TypeCode.Object:
                    {
                        if (valueType == null)
                            throw new NetworkSerializationTypeNotFoundException(typeName, messageId, inResponseToId);

                        try
                        {
                            if (valueType == typeof(byte[]))
                                value = content;
                            else
                            {
                                using (var ms = new MemoryStream(content))
                                    value = _config.Serializer.Deserialize(ms, valueType);

                                if (value != null && !valueType.IsInstanceOfType(value))
                                    throw new SerializationException($"{_config.Serializer.GetType().Name}.Deserialize for type {valueType.Name} returns value of type {value.GetType().Name}.");
                            }
                        }
                        catch (Exception e)
                        {
                            throw new NetworkSerializationException(typeName, e, messageId, inResponseToId);
                        }
                    }
                    break;

                default:
                    value = encoding.GetString(content);
                    value = Convert.ChangeType(value, typeCode, CultureInfo.InvariantCulture);
                    break;
            }

            return messageId == 0 && inResponseToId == 0
                ? value
                : new TrackMessage() { MessageId = messageId, InResponseToId = inResponseToId, Value = value };
        }

        private Task WriteAsync(byte[] buffer) => WriteAsync(buffer, 0, buffer.Length);
        private async Task WriteAsync(byte[] buffer, int offset, int count)
        {
            //const int packLength = 2048;
            //for (int i = offset; i + count < buffer.Length; )
            //{
            //    int left = buffer.Length - i;
            //    int count = packLength < left
            //       ? packLength
            //       : left;

            //    Write(buffer, i, count);
            //    i += count;
            //}

            var stream = Stream;
            if (stream == null)
                return;

            await stream.WriteAsync(buffer, offset, count, m_token).ConfigureAwait(false);
            await stream.FlushAsync(m_token).ConfigureAwait(false);
        }

        private Task ReadAsync(byte[] buffer) => ReadAsync(buffer, 0, buffer.Length);
        private Task ReadAsync(byte[] buffer, int count) => ReadAsync(buffer, 0, count);
        private async Task ReadAsync(byte[] buffer, int offset, int count)
        {
            for (var i = 0; i < count;)
            {
                var stream = Stream;
                if (stream == null)
                    return;

                stream.ReadTimeout = Timeout.Infinite;
                var read = await stream.ReadAsync(buffer, offset + i, count - i, m_token).ConfigureAwait(false);
                if (read == 0)
                    throw new NetworkConnectionBreakException();

                i += read;
            }
        }

        #endregion

        #region IDisposable

        /// <summary>
        /// Closes the connection. It waits for the end of the data transfer.
        /// </summary>
        public void Close()
        {
            if (!IsDisposed)
                // ReSharper disable once MethodSupportsCancellation
                m_writeSemaphore.Wait();

            lock (m_disposeLocker)
                if (!IsDisposed)
                    Dispose(null);
        }
        /// <summary>
        /// Disposes the object without waiting for the end of the data transfer.
        /// </summary>
        public void Dispose()
        {
            lock (m_disposeLocker)
                Dispose(null);
        }

        private void Dispose(Exception ex)
        {
            Dispose(ex, true);
            GC.SuppressFinalize(this);
        }
        private void Dispose(Exception ex, bool disposing)
        {
            if (!IsDisposed)
            {
                m_listening = false;

                if (Stream != null)
                    try { Stream.Dispose(); }
                    catch { /* ignored */ }
                    finally { Stream = null; }

                if (TcpClient != null)
                    try { TcpClient.Close(); }
                    catch { /* ignored */ }
                    finally { TcpClient = null; }

                if (m_readSemaphore != null)
                    try { m_readSemaphore.Dispose(); }
                    catch { /* ignored */ }
                    finally { m_readSemaphore = null; }

                if (m_writeSemaphore != null)
                    try { m_writeSemaphore.Dispose(); }
                    catch { /* ignored */ }
                    finally { m_writeSemaphore = null; }

                if (m_tokenSource != null)
                    try
                    {
                        m_tokenSource.Cancel();
                        m_tokenSource.Dispose();
                    }
                    catch { /* ignored */ }
                    finally { m_tokenSource = null; }

                lock (m_messages)
                {
                    foreach (var message in m_messages)
                    {
                        message.Exception = new NetworkControllerInvocationException(NetworkControllerInvocationExceptionType.OperationCancelled, null);
                        message.Semaphore.Release();
                    }

                    m_messages.Clear();
                }

                IsDisposed = true;

                var ce = Closed;
                Closed = null;
                ce?.Invoke(this, new NetworkConnectionClosedEventArgs(ex));
            }
        }

        private void ThrowIfDisposed()
        {
            if (IsDisposed)
                throw new ObjectDisposedException(null);
        }

        #endregion

        #region Classes

        private class TrackMessage
        {
            public int MessageId { get; set; }
            public int InResponseToId { get; set; }
            public object Value { get; set; }
        }

        [Serializable]
        private class ControllerMethodInvokeModel
        {
            public string ControllerTypeFullName { get; set; }
            public string MethodName { get; set; }
            public string[] ParametersTypesFullNames { get; set; }
            public object[] Parameters { get; set; }
        }
        [Serializable]
        private class ControllerMethodInvokeResultModel
        {
            public int ExceptionCode { get; set; }
            public string ExceptionMessage { get; set; }
            public string FullException { get; set; }
            public object Result { get; set; }
        }

#if NET45
        private class ControllerProxy : System.Runtime.Remoting.Proxies.RealProxy, System.Runtime.Remoting.IRemotingTypeInfo
        {
            private NetworkConnection m_connection;
            private Type m_controllerType;

            public string TypeName
            {
                get { return m_controllerType.FullName; }
                set { throw new NotSupportedException(); }
            }

            public ControllerProxy(Type controllerType, NetworkConnection connection)
                : base(controllerType)
            {
                m_controllerType = controllerType;
                m_connection = connection;
            }


            public bool CanCastTo(Type fromType, object o)
            {
                return fromType == m_controllerType;
            }
            public override System.Runtime.Remoting.Messaging.IMessage Invoke(System.Runtime.Remoting.Messaging.IMessage msg)
            {
                if (msg is System.Runtime.Remoting.Messaging.IMethodCallMessage callMsg)
                {
                    var methodInfo = callMsg.MethodBase as MethodInfo;
                    return new System.Runtime.Remoting.Messaging.ReturnMessage(Invoke(methodInfo, callMsg.Args), null, 0, callMsg.LogicalCallContext, callMsg);
                }

                return null;
            }
            protected object Invoke(MethodInfo targetMethod, object[] args)
            {
                if (typeof(Task).IsAssignableFrom(targetMethod.ReturnType))
                    return m_connection.ControllerMethodInvoke(m_controllerType, targetMethod, args);
        
                var task = m_connection.ControllerMethodInvoke(m_controllerType, targetMethod, args);
                task.Wait();
                return task.Result;
            }

            public static T Create<T>(NetworkConnection connection)
            {
                var proxy = new ControllerProxy(typeof(T), connection);
                return (T)proxy.GetTransparentProxy();
            }
        }
#else
        public class ControllerProxy : DispatchProxy
        {
            private NetworkConnection m_connection;
            private Type m_controllerType;


            protected override object Invoke(MethodInfo targetMethod, object[] args)
            {
                if (typeof(Task).IsAssignableFrom(targetMethod.ReturnType))
                    return m_connection.ControllerMethodInvoke(m_controllerType, targetMethod, args);

                var task = m_connection.ControllerMethodInvoke(m_controllerType, targetMethod, args);
                task.Wait();
                return task.Result;
            }

            public static T Create<T>(NetworkConnection connection)
            {
                var proxy = Create<T, ControllerProxy>();
                ((ControllerProxy)(object)proxy).m_connection = connection;
                ((ControllerProxy)(object)proxy).m_controllerType = typeof(T);
                return proxy;
            }
        }
#endif
        private class ControllerMethodInvokeState
        {
            public int MessageId { get; set; }
            public SemaphoreSlim Semaphore { get; set; }
            public object Result { get; set; }
            public Exception Exception { get; set; }
        }

        #endregion
    }
}
