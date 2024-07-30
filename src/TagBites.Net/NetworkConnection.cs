using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Net.Security;
using System.Net.Sockets;
using System.Reflection;
using System.Runtime.Serialization;
using System.Text;

namespace TagBites.Net;

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

    private readonly object _disposeLocker = new();
    private SemaphoreSlim _readSemaphore = new(1);
    private SemaphoreSlim _writeSemaphore = new(1);
    private readonly CancellationToken _token;
    private CancellationTokenSource _tokenSource;

    private bool _listening;
    private Task _listeningTask;

    private int _messageId;
    private readonly List<ControllerMethodInvokeState> _messages = new();
    private readonly List<object> _remoteControllers = new();
    private readonly Dictionary<string, object> _controllers = new();
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
        get => _listening && !IsDisposed;
        set
        {
            if (IsDisposed)
                throw new ObjectDisposedException(null);

            if (_listening != value)
            {
                if (value)
                {
                    _listeningTask = _listeningTask != null
                        ? _listeningTask.ContinueWith(t => ListeningCore())
                        : Task.Run(ListeningCore);
                }

                _listening = value;
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

        _tokenSource = new CancellationTokenSource();
        _token = _tokenSource.Token;
    }
    /// <inheritdoc />
    ~NetworkConnection()
    {
        lock (_disposeLocker)
            Dispose(null, false);
    }


    private async Task ListeningCore()
    {
        while (Listening)
        {
            try
            {
                var message = await ReadAsyncCore();
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

        lock (_remoteControllers)
        {
            var controller = _remoteControllers.FirstOrDefault(x => x is T);
            if (controller == null)
            {
                controller = ControllerProxy.Create<T>(this);
                _remoteControllers.Add(controller);
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

        lock (_controllers)
            _controllers[name] = controller;
    }

    private async void OnTrackMessage(TrackMessage message)
    {
        if (message.InResponseToId != 0)
            OnTrackMessageResponse(message);
        else
        {
            var response = new TrackMessage
            {
                MessageId = Interlocked.Increment(ref _messageId),
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
        lock (_messages)
        {
            for (var i = 0; i < _messages.Count; i++)
            {
                var msg = _messages[i];
                if (msg.MessageId == message.InResponseToId)
                {
                    _messages.RemoveAt(i);

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
        lock (_messages)
        {
            for (var i = 0; i < _messages.Count; i++)
            {
                var msg = _messages[i];
                if (msg.MessageId == inResponseToId)
                {
                    _messages.RemoveAt(i);

                    msg.Exception = new NetworkControllerInvocationException(NetworkControllerInvocationExceptionType.DataReceivingError, null, error);
                    msg.Semaphore.Release();

                    break;
                }
            }
        }
    }

    private async Task<object> ControllerMethodInvoke(Type controllerType, MethodInfo method, object[] parameters)
    {
        var model = new ControllerMethodInvokeModel
        {
            ControllerTypeFullName = controllerType.FullName + ", " + controllerType.Assembly.GetName().Name,
            MethodName = method.Name,
            ParametersTypesFullNames = method.GetParameters().Select(x => x.ParameterType.FullName).ToArray(),
            Parameters = parameters
        };
        var message = new TrackMessage
        {
            MessageId = Interlocked.Increment(ref _messageId),
            Value = model
        };
        var info = new ControllerMethodInvokeState
        {
            MessageId = message.MessageId,
            Semaphore = new SemaphoreSlim(0)
        };
        lock (_messages)
            _messages.Add(info);

        await WriteAsync(message).ConfigureAwait(false);
        await info.Semaphore.WaitAsync(_token).ConfigureAwait(false);

        if (info.Exception != null)
            throw info.Exception;

        return info.Result;
    }
    private async Task<ControllerMethodInvokeResultModel> RemoteControllerMethodInvoke(int messageId, ControllerMethodInvokeModel invokeModel)
    {
        var result = new ControllerMethodInvokeResultModel();

        // Find controller
        object controller;
        lock (_controllers)
        {
            var controllerName = invokeModel.ControllerTypeFullName;

            if (!_controllers.TryGetValue(controllerName, out controller))
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
                                _controllers[controllerName] = controller;
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
            await _readSemaphore.WaitAsync(_token).ConfigureAwait(false);
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
                        Task.Run(() => OnTrackMessage(m), _token);
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
                        Task.Run(() => OnTrackMessageResponseError(e.InResponseToId, e), _token);
#pragma warning restore 4014
                        continue;
                    }
                    else if (e.MessageId != 0)
                    {
#pragma warning disable 4014
                        Task.Run(async () =>
                        {
                            var trackMessage = new TrackMessage
                            {
                                InResponseToId = e.MessageId,
                                Value = new ControllerMethodInvokeResultModel
                                {
                                    ExceptionCode = (int)NetworkControllerInvocationExceptionType.DataReceivingError,
                                    ExceptionMessage = e.Message,
                                    FullException = e.ToString()
                                }
                            };
                            await WriteAsync(trackMessage);
                        }, _token);
#pragma warning restore 4014
                    }

                    throw;
                }
                catch (NetworkConnectionBreakException e)
                {
                    lock (_disposeLocker)
                    {
                        if (!IsDisposed)
                            Dispose(e);

                        throw;
                    }
                }
                catch (Exception e)
                {
                    lock (_disposeLocker)
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
                _readSemaphore?.Release();
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
            await _writeSemaphore.WaitAsync(_token).ConfigureAwait(false);
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
                lock (_disposeLocker)
                {
                    if (!IsDisposed)
                        Dispose(e);

                    throw;
                }
            }
            catch (Exception e)
            {
                lock (_disposeLocker)
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
                _writeSemaphore?.Release();
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
                : new TrackMessage { MessageId = messageId, InResponseToId = inResponseToId };
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
            : new TrackMessage { MessageId = messageId, InResponseToId = inResponseToId, Value = value };
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

        await stream.WriteAsync(buffer, offset, count, _token).ConfigureAwait(false);
        await stream.FlushAsync(_token).ConfigureAwait(false);
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
            var read = await stream.ReadAsync(buffer, offset + i, count - i, _token).ConfigureAwait(false);
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
            _writeSemaphore.Wait();

        lock (_disposeLocker)
            if (!IsDisposed)
                Dispose(null);
    }
    /// <summary>
    /// Disposes the object without waiting for the end of the data transfer.
    /// </summary>
    public void Dispose()
    {
        lock (_disposeLocker)
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
            _listening = false;

            if (Stream != null)
                try { Stream.Dispose(); }
                catch { /* ignored */ }
                finally { Stream = null; }

            if (TcpClient != null)
                try { TcpClient.Close(); }
                catch { /* ignored */ }
                finally { TcpClient = null; }

            if (_readSemaphore != null)
                try { _readSemaphore.Dispose(); }
                catch { /* ignored */ }
                finally { _readSemaphore = null; }

            if (_writeSemaphore != null)
                try { _writeSemaphore.Dispose(); }
                catch { /* ignored */ }
                finally { _writeSemaphore = null; }

            if (_tokenSource != null)
                try
                {
                    _tokenSource.Cancel();
                    _tokenSource.Dispose();
                }
                catch { /* ignored */ }
                finally { _tokenSource = null; }

            lock (_messages)
            {
                foreach (var message in _messages)
                {
                    message.Exception = new NetworkControllerInvocationException(NetworkControllerInvocationExceptionType.OperationCancelled, null);
                    message.Semaphore.Release();
                }

                _messages.Clear();
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
            private NetworkConnection _connection;
            private Type _controllerType;

            public string TypeName
            {
                get { return _controllerType.FullName; }
                set { throw new NotSupportedException(); }
            }

            public ControllerProxy(Type controllerType, NetworkConnection connection)
                : base(controllerType)
            {
                _controllerType = controllerType;
                _connection = connection;
            }


            public bool CanCastTo(Type fromType, object o)
            {
                return fromType == _controllerType;
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
                    return _connection.ControllerMethodInvoke(_controllerType, targetMethod, args);
        
                var task = _connection.ControllerMethodInvoke(_controllerType, targetMethod, args);
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
        private NetworkConnection _connection;
        private Type _controllerType;


        protected override object Invoke(MethodInfo targetMethod, object[] args)
        {
            var task = _connection.ControllerMethodInvoke(_controllerType, targetMethod, args);

            if (typeof(Task).IsAssignableFrom(targetMethod.ReturnType) && targetMethod.ReturnType != typeof(Task))
            {
                return typeof(ControllerProxy)
                    .GetMethod(nameof(ChangeTaskType), BindingFlags.Static | BindingFlags.NonPublic)!
                    .MakeGenericMethod(GetGenericResultType(targetMethod.ReturnType))
                    .Invoke(null, new object[] { task });
            }

            return task.GetAwaiter().GetResult();
        }

        public static T Create<T>(NetworkConnection connection)
        {
            var proxy = Create<T, ControllerProxy>();
            ((ControllerProxy)(object)proxy)._connection = connection;
            ((ControllerProxy)(object)proxy)._controllerType = typeof(T);
            return proxy;
        }

        private static async Task<T> ChangeTaskType<T>(Task<object> executeTask)
        {
            var result = await executeTask;
            return result is T t ? t : default;
        }
        private static Type GetGenericResultType(Type taskType)
        {
            for (var type = taskType; type != typeof(Task); type = type.BaseType)
            {
                if (type!.IsGenericType && type.GetGenericTypeDefinition() == typeof(Task<>))
                    return type.GetGenericArguments()[0];
            }

            return null;
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
