/* License
 * --------------------------------------------------------------------------------------------------------------------
 * This file is part of the AI4E distribution.
 *   (https://github.com/AI4E/AI4E)
 * Copyright (c) 2018 - 2019 Andreas Truetschel and contributors.
 * 
 * AI4E is free software: you can redistribute it and/or modify  
 * it under the terms of the GNU Lesser General Public License as   
 * published by the Free Software Foundation, version 3.
 *
 * AI4E is distributed in the hope that it will be useful, but 
 * WITHOUT ANY WARRANTY; without even the implied warranty of 
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU 
 * Lesser General Public License for more details.
 *
 * You should have received a copy of the GNU Lesser General Public License
 * along with this program. If not, see <http://www.gnu.org/licenses/>.
 * --------------------------------------------------------------------------------------------------------------------
 */

#nullable disable

using System;
using System.Buffers;
using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using AI4E.Utils.Async;
using AI4E.Utils.Processing;
using Microsoft.Extensions.DependencyInjection;
using Nito.AsyncEx;

namespace AI4E.Utils.Proxying
{
    /// <summary>
    /// Represents a proxy host.
    /// </summary>
    public sealed class ProxyHost : IAsyncDisposable, IProxyHost
    {
        private static readonly MethodInfo _createProxyMethodDefinition =
            typeof(ProxyHost)
            .GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)
            .SingleOrDefault(p => p.Name == nameof(CreateProxy) && p.IsGenericMethodDefinition && p.GetGenericArguments().Length == 1);

        #region Fields

#pragma warning disable IDE0069, CA2213
        private readonly Stream _stream;
#pragma warning restore IDE0069, CA2213
        private readonly IServiceProvider _serviceProvider;
        private readonly AsyncLock _sendLock = new AsyncLock();
        private readonly IAsyncProcess _receiveProcess;
        private readonly ConcurrentDictionary<int, (Action<MessageType, object> callback, Type resultType)> _responseTable
            = new ConcurrentDictionary<int, (Action<MessageType, object> callback, Type resultType)>();
        private readonly Dictionary<object, IProxyInternal> _proxyLookup = new Dictionary<object, IProxyInternal>();
        private readonly Dictionary<int, IProxyInternal> _proxies = new Dictionary<int, IProxyInternal>();
        private readonly object _proxyLock = new object();
        private readonly Dictionary<int, IProxyInternal> _remoteProxies = new Dictionary<int, IProxyInternal>();
        private readonly object _remoteProxiesMutex = new object();
        private readonly object _cancellationTokenSourcesMutex = new object();
        private readonly Dictionary<int, Dictionary<int, CancellationTokenSource>> _cancellationTokenSources
            = new Dictionary<int, Dictionary<int, CancellationTokenSource>>();
        private static volatile ImmutableList<Type> _loadedRemoteTypes = ImmutableList<Type>.Empty;
        private static readonly HashSet<Type> _loadedTransparentProxyTypes = new HashSet<Type>();
        private static readonly object _loadedTransparentProxyTypesMutex = new object();
        private readonly AsyncDisposeHelper _disposeHelper;

        private int _nextSeqNum = 0;
        private int _nextLocalProxyId = 0;
        private int _nextRemoteProxyId = 0;

        #endregion

        #region Ctor

#nullable enable
        /// <summary>
        /// Creates a new instance of the <see cref="ProxyHost"/> type.
        /// </summary>
        /// <param name="stream">A <see cref="Stream"/> that is used to communicate with the remote end-point.</param>
        /// <param name="serviceProvider">A <see cref="IServiceProvider"/> that is used to resolve services.</param>
        public ProxyHost(Stream stream, IServiceProvider serviceProvider)
#nullable disable
        {
            if (stream == null)
                throw new ArgumentNullException(nameof(stream));

            if (serviceProvider == null)
                throw new ArgumentNullException(nameof(serviceProvider));

            _stream = stream;
            _serviceProvider = serviceProvider;

            _receiveProcess = new AsyncProcess(ReceiveProcess, start: true);
            _disposeHelper = new AsyncDisposeHelper(DisposeInternalAsync);
        }

        #endregion

        #region Activation

#nullable enable
        /// <inheritdoc />
        public Task<IProxy<TRemote>> CreateAsync<TRemote>(object[] parameter, CancellationToken cancellation)
            where TRemote : class
#nullable disable
        {
            return ActivateAsync<TRemote>(ActivationMode.Create, parameter ?? Array.Empty<object>(), cancellation);
        }

#nullable enable
        /// <inheritdoc />
        public Task<IProxy<TRemote>> CreateAsync<TRemote>(CancellationToken cancellation)
            where TRemote : class
#nullable disable
        {
            return ActivateAsync<TRemote>(ActivationMode.Create, Array.Empty<object>(), cancellation);
        }

#nullable enable
        /// <inheritdoc />
        public Task<IProxy<TRemote>> LoadAsync<TRemote>(CancellationToken cancellation)
            where TRemote : class
#nullable disable
        {
            return ActivateAsync<TRemote>(ActivationMode.Load, parameter: null, cancellation);
        }

#nullable enable
        /// <inheritdoc />
        public IProxy<TRemote> Create<TRemote>(object[] parameter)
            where TRemote : class
#nullable disable
        {
            var proxy = new Proxy<TRemote>(this, GenerateRemoteProxyId(), ActivationMode.Create, parameter);

            lock (_remoteProxiesMutex)
            {
                _remoteProxies.Add(proxy.Id, proxy);
            }

            return proxy;
        }

#nullable enable
        /// <inheritdoc />
        public IProxy<TRemote> Create<TRemote>()
            where TRemote : class
#nullable disable
        {
            var proxy = new Proxy<TRemote>(this, GenerateRemoteProxyId(), ActivationMode.Create, activationParameters: null);

            lock (_remoteProxiesMutex)
            {
                _remoteProxies.Add(proxy.Id, proxy);
            }

            return proxy;
        }

#nullable enable
        /// <inheritdoc />
        public IProxy<TRemote> Load<TRemote>()
            where TRemote : class
#nullable disable
        {
            var proxy = new Proxy<TRemote>(this, GenerateRemoteProxyId(), ActivationMode.Load, activationParameters: null);

            lock (_remoteProxiesMutex)
            {
                _remoteProxies.Add(proxy.Id, proxy);
            }

            return proxy;
        }

        internal Task<IProxy<TRemote>> ActivateAsync<TRemote>(ActivationMode mode, object[] parameter, CancellationToken cancellation)
            where TRemote : class
        {
            var id = GenerateRemoteProxyId();
            return ActivateAsync<TRemote>(mode, parameter, id, cancellation);
        }

        internal async Task<IProxy<TRemote>> ActivateAsync<TRemote>(ActivationMode mode, object[] parameter, int id, CancellationToken cancellation)
            where TRemote : class
        {
            int seqNum;
            Task<IProxy<TRemote>> result;

            using (var stream = new MemoryStream())
            {
                do
                {
                    seqNum = Interlocked.Increment(ref _nextSeqNum);

                    stream.Position = 0;
                    stream.SetLength(0);

                    using var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true);
                    writer.Write((byte)MessageType.Activation);
                    writer.Write(seqNum);

                    writer.Write(id);
                    writer.Write((byte)mode);
                    WriteType(writer, typeof(TRemote));
                    Serialize(writer, parameter?.Select(p => (p, p?.GetType())), null);
                }
                while (!TryGetResultTask(seqNum, out result));

                stream.Position = 0;
                await SendAsync(stream, cancellation).ConfigureAwait(false);
            }

            return await result.ConfigureAwait(false);
        }

        internal async Task Deactivate(int proxyId, CancellationToken cancellation)
        {
            try
            {
                var seqNum = Interlocked.Increment(ref _nextSeqNum);

                using var stream = new MemoryStream();
                using (var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true))
                {
                    writer.Write((byte)MessageType.Deactivation);
                    writer.Write(seqNum);
                    writer.Write(proxyId);
                }

                stream.Position = 0;
                await SendAsync(stream, cancellation).ConfigureAwait(false);
            }
            catch (ObjectDisposedException) { }
        }

        private int GenerateRemoteProxyId()
        {
            var id = Interlocked.Increment(ref _nextRemoteProxyId);

            // Ids for remote proxies we create must carry a one in the MSB to prevent id conflicts.
            id = -id;

            return id;
        }

        #endregion

        #region Disposal

#nullable enable
        /// <inheritdoc />
        public Task Disposal
#nullable disable
            => _disposeHelper.Disposal;

#nullable enable
        /// <inheritdoc />
        public void Dispose()
#nullable disable
        {
            _disposeHelper.Dispose();
        }

#nullable enable
        /// <inheritdoc />
        public ValueTask DisposeAsync()
#nullable disable
        {
            return _disposeHelper.DisposeAsync();
        }

        private async Task DisposeInternalAsync()
        {
            await _receiveProcess.TerminateAsync().HandleExceptionsAsync().ConfigureAwait(false);

            List<IProxyInternal> proxies;

            lock (_proxyLock)
            {
                proxies = _proxies.Values.ToList();
            }

            lock (_remoteProxiesMutex)
            {
                proxies.AddRange(_remoteProxies.Values);
            }

            var objectDisposedException = new ObjectDisposedException(GetType().FullName);

            // TODO: Parallelize this.
            foreach (var callback in _responseTable.Values.Select(p => p.callback))
            {
                callback.Invoke(MessageType.ReturnException, objectDisposedException);
            }

            await Task.WhenAll(proxies.Select(p => p.DisposeAsync().AsTask()))
                .HandleExceptionsAsync()
                .ConfigureAwait(false);

            _stream.Dispose();
        }

        #endregion

        #region Proxies

        private IProxyInternal RegisterLocalProxy(IProxyInternal proxy, int id)
        {
            try
            {
                using (_disposeHelper.GuardDisposal())
                {
                    lock (_proxyLock)
                    {
                        if (_proxyLookup.TryGetValue(proxy.LocalInstance, out var existing))
                        {
                            return existing;
                        }

                        proxy.Register(this, id, () => UnregisterLocalProxy(proxy));

                        _proxyLookup.Add(proxy.LocalInstance, proxy);
                        _proxies.Add(id, proxy);
                    }
                }
            }
            catch (OperationCanceledException) when (_disposeHelper.IsDisposed)
            {
                throw new ObjectDisposedException(GetType().FullName);
            }

            return proxy;
        }

        private IProxyInternal RegisterLocalProxy(IProxyInternal proxy)
        {
            var id = Interlocked.Increment(ref _nextLocalProxyId);

            return RegisterLocalProxy(proxy, id);
        }

        private void UnregisterLocalProxy(IProxyInternal proxy)
        {
            lock (_proxyLock)
            {
                _proxyLookup.Remove(proxy.LocalInstance);
                _proxies.Remove(proxy.Id);
            }
        }

        private bool TryGetProxyById(int proxyId, out IProxyInternal proxy)
        {
            lock (_proxyLock)
            {
                return _proxies.TryGetValue(proxyId, out proxy);
            }
        }

        /// <summary>
        /// Gets a collection of registered local proxies.
        /// FOR TEST AND DEBUGGING PUPOSES ONLY.
        /// </summary>
        internal IReadOnlyCollection<IProxyInternal> LocalProxies
        {
            get
            {
                lock (_proxyLock)
                {
                    return _proxies.Values.ToImmutableList();
                }
            }
        }

#nullable enable
        /// <summary>
        /// Creates a new proxy from the specified object instance.
        /// </summary>
        /// <typeparam name="TRemote">The type of object that a proxy is created for.</typeparam>
        /// <param name="instance">The instance a proxy is created for.</param>
        /// <param name="ownsInstance">A boolean value indicating whether the proxy host owns the instance.</param>
        /// <returns>The create proxy.</returns>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="instance"/> is <c>null</c>.</exception>
        public static IProxy<TRemote> CreateProxy<TRemote>(TRemote instance, bool ownsInstance = false)
            where TRemote : class
#nullable disable
        {
            if (instance == null)
                throw new ArgumentNullException(nameof(instance));

            return new Proxy<TRemote>(instance, ownsInstance);
        }

        internal static IProxyInternal CreateProxy(Type remoteType, object instance, bool ownsInstance)
        {
            var createProxyMethod = _createProxyMethodDefinition.MakeGenericMethod(remoteType);
            return (IProxyInternal)createProxyMethod.Invoke(obj: null, new object[] { instance, ownsInstance });
        }

        internal static void AddTransparentProxyType(Type type)
        {
            lock (_loadedTransparentProxyTypesMutex)
            {
                _loadedTransparentProxyTypes.Add(type);
            }
        }

        internal static void AddLoadedRemoteType(Type remoteType)
        {
            _loadedRemoteTypes = _loadedRemoteTypes.Add(remoteType); // Volatile write op.
        }

        #endregion

        #region Receive

        private async Task ReceiveProcess(CancellationToken cancellation)
        {
            var messageLengthBytes = new byte[4];
            while (cancellation.ThrowOrContinue())
            {
                try
                {
                    try
                    {
                        await _stream.ReadExactAsync(
                            messageLengthBytes, offset: 0, count: messageLengthBytes.Length, cancellation)
                            .ConfigureAwait(false);
                        var messageLength = BinaryPrimitives.ReadInt32LittleEndian(messageLengthBytes.AsSpan());

                        var buffer = ArrayPool<byte>.Shared.Rent(messageLength);
                        try
                        {
                            await _stream.ReadExactAsync(buffer, offset: 0, count: messageLength, cancellation)
                                .ConfigureAwait(false);

                            // We do not want the process to be disturbed/blocked/deadlocked
                            Task.Run(async () =>
                            {
                                try
                                {
                                    using var messageStream = new MemoryStream(
                                        buffer, index: 0, count: messageLength, writable: false);
                                    await HandleMessageAsync(messageStream, cancellation).ConfigureAwait(false);
                                }
                                finally
                                {
                                    ArrayPool<byte>.Shared.Return(buffer);
                                }

                            }).HandleExceptions();
                        }
                        catch
                        {
                            ArrayPool<byte>.Shared.Return(buffer);
                            throw;
                        }
                    }
                    catch (ObjectDisposedException)
                    {
                        // Do not call DisposeAsync. This will result in a deadlock.
                        Dispose();
                        return;
                    }
                    catch (IOException)
                    {
                        // Do not call DisposeAsync. This will result in a deadlock.
                        Dispose();
                        return;
                    }
                }
                catch (OperationCanceledException) when (cancellation.IsCancellationRequested) { throw; }
#pragma warning disable CA1031
                catch
#pragma warning restore CA1031
                {
                    // TODO: Log exception
                }
            }
        }

        private async Task HandleMessageAsync(Stream stream, CancellationToken cancellation)
        {
            using var reader = new BinaryReader(stream, Encoding.UTF8, leaveOpen: false);
            var messageType = (MessageType)reader.ReadByte();
            var seqNum = reader.ReadInt32();

            switch (messageType)
            {
                case MessageType.ReturnValue:
                case MessageType.ReturnException:
                    ReceiveResult(messageType, reader);
                    break;

                case MessageType.MethodCall:
                    await ReceiveMethodCallAsync(reader, seqNum, cancellation).ConfigureAwait(false);
                    break;

                case MessageType.Activation:
                    await ReceiveActivationAsync(reader, seqNum, cancellation).ConfigureAwait(false);
                    break;

                case MessageType.Deactivation:
                    ReceiveDeactivation(reader);
                    break;

                case MessageType.CancellationRequest:
                    ReceiveCancellationRequest(reader);
                    break;
            }
        }

        private void ReceiveCancellationRequest(BinaryReader reader)
        {
            var corr = reader.ReadInt32();
            var cancellationTokenId = reader.ReadInt32();

            lock (_cancellationTokenSourcesMutex)
            {
                if (_cancellationTokenSources.TryGetValue(corr, out var cancellationTokenSources) &&
                   cancellationTokenSources.TryGetValue(cancellationTokenId, out var cancellationTokenSource))
                {
                    cancellationTokenSource.Cancel();
                }
            }
        }

        private void ReceiveDeactivation(BinaryReader reader)
        {
            var proxyId = reader.ReadInt32();

            if (TryGetProxyById(proxyId, out var proxy))
            {
                proxy.Dispose();
            }
        }

        private async Task ReceiveActivationAsync(BinaryReader reader, int seqNum, CancellationToken cancellation)
        {
            var result = default(object);
            var exception = default(Exception);
            var activatedProxies = new List<(int id, Type objectType)>();

            try
            {
                var id = reader.ReadInt32();
                var mode = (ActivationMode)reader.ReadByte();
                var type = ReadType(reader);
                var parameter = Deserialize(reader, (ParameterInfo[])null, null, activatedProxies).ToArray();
                var instance = default(object);
                var ownsInstance = false;

                if (mode == ActivationMode.Create)
                {
                    instance = ActivatorUtilities.CreateInstance(_serviceProvider, type, parameter);
                    ownsInstance = true;
                }
                else if (mode == ActivationMode.Load)
                {
                    instance = _serviceProvider.GetRequiredService(type);
                }

                var proxy = (IProxyInternal)Activator.CreateInstance(
                    typeof(Proxy<>).MakeGenericType(type),
                    BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public,
                    null,
                    new[] { instance, ownsInstance },
                    null);
                result = RegisterLocalProxy(proxy);
            }
            catch (TargetInvocationException exc)
            {
                exception = exc.InnerException;
            }
#pragma warning disable CA1031
            catch (Exception exc)
#pragma warning restore CA1031
            {
                exception = exc;
            }

            await SendResult(seqNum, result, result.GetType(), exception, waitTask: false, activatedProxies, cancellation)
                .ConfigureAwait(false);
        }

        private async Task ReceiveMethodCallAsync(BinaryReader reader, int seqNum, CancellationToken cancellation)
        {
            var result = default(object);
            var exception = default(Exception);
            var waitTask = false;

            MethodInfo method;
            Type returnType = null;

            var cancellationTokenSources = new Dictionary<int, CancellationTokenSource>();
            var activatedProxies = new List<(int id, Type objectType)>();

            lock (_cancellationTokenSourcesMutex)
            {
                _cancellationTokenSources[seqNum] = cancellationTokenSources;
            }

            try
            {
                try
                {
                    var proxyId = reader.ReadInt32();
                    var isActivated = reader.ReadBoolean();

                    IProxyInternal proxy;

                    if (isActivated)
                    {
                        if (!TryGetProxyById(proxyId, out proxy))
                        {
                            throw new Exception("Proxy not found."); // TODO
                        }
                    }
                    else
                    {
                        var proxyType = LoadTypeIgnoringVersion(reader.ReadString());
                        var mode = (ActivationMode)reader.ReadByte();
                        var parameters = Deserialize(reader, (ParameterInfo[])null, null, activatedProxies).ToArray();

                        object i;
                        var ownsInstance = false;

                        if (mode == ActivationMode.Create)
                        {
                            i = ActivatorUtilities.CreateInstance(_serviceProvider, proxyType, parameters);
                            ownsInstance = true;
                        }
                        else // if (mode == ActivationMode.Load)
                        {
                            i = _serviceProvider.GetRequiredService(proxyType);
                        }

                        proxy = (IProxyInternal)Activator.CreateInstance(typeof(Proxy<>).MakeGenericType(proxyType), BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public, null, new[] { i, ownsInstance }, null);
                        proxy = RegisterLocalProxy(proxy, proxyId);

                        activatedProxies.Add((proxy.Id, proxy.LocalInstance.GetType()));
                    }

                    waitTask = reader.ReadBoolean();
                    method = DeserializeMethod(reader);

                    var arguments = Deserialize(
                        reader,
                        method.GetParameters(),
                        cancellationTokenSources,
                        activatedProxies).ToArray();

                    var instance = proxy.LocalInstance;

                    if (instance == null)
                    {
                        throw new Exception("Proxy not found."); // TODO
                    }

                    result = method.Invoke(instance, arguments);
                    returnType = method.ReturnType;
                }
                catch (TargetInvocationException exc)
                {
                    exception = exc.InnerException;
                }
#pragma warning disable CA1031
                catch (Exception exc)
#pragma warning restore CA1031
                {
                    exception = exc;
                }

                await SendResult(seqNum, result, returnType, exception, waitTask, activatedProxies, cancellation)
                    .ConfigureAwait(false);
            }
            finally
            {
                lock (_cancellationTokenSourcesMutex)
                {
                    _cancellationTokenSources.Remove(seqNum);
                }
            }
        }

        private void ReceiveResult(MessageType messageType, BinaryReader reader)
        {
            var corr = reader.ReadInt32();
            var activatedProxiesCount = reader.ReadInt32();

            for (var i = 0; i < activatedProxiesCount; i++)
            {
                var id = reader.ReadInt32();
                var objectType = LoadTypeIgnoringVersion(reader.ReadString());

                lock (_remoteProxiesMutex)
                {
                    if (_remoteProxies.TryGetValue(id, out var proxy))
                    {
                        proxy.Activate(objectType);
                    }
                }
            }

            if (_responseTable.TryRemove(corr, out var entry))
            {
                var value = Deserialize(reader, expectedType: entry.resultType, null, new List<(int id, Type objectType)>() { });
                entry.callback(messageType, value);
            }
        }

        #endregion

        #region Send

        private readonly byte[] _sendMessageLengthBuffer = new byte[4];

        private async Task SendAsync(Stream stream, CancellationToken cancellation)
        {
            using (await _sendLock.LockAsync())
            {
                var messageLength = checked((int)stream.Length);
                BinaryPrimitives.WriteInt32LittleEndian(_sendMessageLengthBuffer.AsSpan(), messageLength);

                try
                {
                    await _stream.WriteAsync(
                        _sendMessageLengthBuffer, offset: 0, count: _sendMessageLengthBuffer.Length)
                        .ConfigureAwait(false);

                    await stream.CopyToAsync(_stream, bufferSize: 81920, cancellation)
                        .ConfigureAwait(false);
                }
                catch (ObjectDisposedException)
                {
                    Dispose();
                    throw;
                }
            }
        }

        private async Task SendResult(
            int corrNum,
            object result,
            Type resultType,
            Exception exception,
            bool waitTask,
            List<(int id, Type objectType)> activatedProxies,
            CancellationToken cancellation)
        {
            if (exception == null && waitTask)
            {
                try
                {
                    var task = (Task)result;
                    await task.ConfigureAwait(false);
                    result = task.GetResultOrDefault();
                    resultType = task.GetResultType();
                }
#pragma warning disable CA1031
                catch (Exception exc)
#pragma warning restore CA1031
                {
                    exception = exc;
                }
            }

            var seqNum = Interlocked.Increment(ref _nextSeqNum);

            using var stream = new MemoryStream();
            using (var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true))
            {
                writer.Write(exception == null ? (byte)MessageType.ReturnValue : (byte)MessageType.ReturnException);
                writer.Write(seqNum);
                writer.Write(corrNum);

                writer.Write(activatedProxies.Count);

                foreach (var (id, objectType) in activatedProxies)
                {
                    writer.Write(id);
                    writer.Write(objectType.AssemblyQualifiedName);
                }

                if (exception != null)
                {
                    Serialize(writer, exception, exception.GetType(), null);
                }
                else
                {
                    Serialize(writer, result, resultType, null);
                }
            }

            stream.Position = 0;
            await SendAsync(stream, cancellation).ConfigureAwait(false);
        }

        internal Task<TResult> SendMethodCallAsync<TResult>(Expression expression, IProxyInternal proxy, bool waitTask)
        {
            var method = default(MethodInfo);
            var parameters = Array.Empty<object>();

            if (expression is MethodCallExpression methodCallExpression)
            {
                method = methodCallExpression.Method;
                parameters = methodCallExpression.Arguments.Select(p => p.Evaluate()).ToArray();
            }
            else if (expression is MemberExpression memberExpression && memberExpression.Member is PropertyInfo property)
            {
                method = property.GetGetMethod();
            }
            else
            {
                throw new InvalidOperationException(); // TODO: What about Property writes? What about indexed properties?
            }

            return SendMethodCallAsync<TResult>(method, parameters, proxy, waitTask);
        }

        internal async Task<TResult> SendMethodCallAsync<TResult>(MethodInfo method, object[] args, IProxyInternal proxy, bool waitTask)
        {
            // TODO: Add sanity checks.

            var seqNum = default(int);
            var task = default(Task<TResult>);

            using var stream = new MemoryStream();
            var cancellationTokens = new List<CancellationToken>();

            do
            {
                seqNum = Interlocked.Increment(ref _nextSeqNum);

                stream.Position = 0;
                stream.SetLength(0);

                using var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true);
                writer.Write((byte)MessageType.MethodCall);
                writer.Write(seqNum);

                writer.Write(proxy.Id);

                var isActivated = proxy.IsActivated;

                writer.Write(isActivated);

                if (!isActivated)
                {
                    var proxyType = proxy.RemoteType;
                    writer.Write(proxyType.AssemblyQualifiedName);
                    writer.Write((byte)proxy.ActivationMode);
                    Serialize(writer, proxy.ActivationParamers?.Select(p => (p, p?.GetType())), null);
                }

                writer.Write(waitTask);
                SerializeMethod(writer, method);
                Serialize(writer, args.Zip(method.GetParameters(), (arg, param) => (arg, param.ParameterType)), cancellationTokens);
                writer.Flush();
            }
            while (!TryGetResultTask(seqNum, out task));

            stream.Position = 0;

            var registrations = new List<CancellationTokenRegistration>(capacity: cancellationTokens.Count);
            var cancellations = new List<Task>();

            using var cancellationOperationCancellation = new CancellationTokenSource();

            List<Exception> exceptions = null;
            TResult result = default;
            try
            {
                for (var id = 0; id < cancellationTokens.Count; id++)
                {
                    var cancellationToken = cancellationTokens[id];
                    if (!cancellationToken.CanBeCanceled)
                    {
                        continue;
                    }

                    if (cancellationToken.IsCancellationRequested)
                    {
                        cancellations.Add(CancelMethodCallAsync(seqNum, id, cancellationOperationCancellation.Token));
                        continue;
                    }

                    var idCopy = id;
                    var registration = cancellationToken.Register(() => cancellations.Add(CancelMethodCallAsync(seqNum, idCopy, cancellationOperationCancellation.Token)));
                    try
                    {
                        if (cancellationToken.IsCancellationRequested)
                        {
                            cancellations.Add(CancelMethodCallAsync(seqNum, id, cancellationOperationCancellation.Token));
                            registration.Dispose();
                            continue;
                        }

                        registrations.Add(registration);
                    }
                    catch
                    {
                        registration.Dispose();
                        throw;
                    }
                }

                await SendAsync(stream, cancellation: default).ConfigureAwait(false);

                result = await task.ConfigureAwait(false);
            }
#pragma warning disable CA1031
            catch (Exception exc)
#pragma warning restore CA1031
            {
                if (exceptions == null)
                    exceptions = new List<Exception>();

                exceptions.Add(exc);
            }

            foreach (var registration in registrations)
            {
                try
                {
                    registration.Dispose();
                }
#pragma warning disable CA1031
                catch (Exception exc)
#pragma warning restore CA1031
                {
                    if (exceptions == null)
                        exceptions = new List<Exception>();

                    exceptions.Add(exc);
                }
            }

            cancellationOperationCancellation.Cancel();

            if (cancellations.Any())
            {
                try
                {
                    await Task.WhenAll(cancellations).ConfigureAwait(false);
                }
#pragma warning disable CA1031
                catch (Exception exc)
#pragma warning restore CA1031
                {
                    if (exceptions == null)
                        exceptions = new List<Exception>();

                    exceptions.Add(exc);
                }
            }

            if (exceptions != null)
            {
                if (exceptions.Count == 1)
                {
                    throw exceptions[0];
                }

                throw new AggregateException(exceptions);
            }

            return result;
        }

        private async Task CancelMethodCallAsync(int corrNum, int cancellationTokenId, CancellationToken cancellation)
        {
            var delay = TimeSpan.FromMilliseconds(200);
            var maxDelay = TimeSpan.FromMilliseconds(1000);

            while (!cancellation.IsCancellationRequested)
            {
                var seqNum = Interlocked.Increment(ref _nextSeqNum);

                using (var stream = new MemoryStream())
                {
                    using (var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true))
                    {
                        writer.Write((byte)MessageType.CancellationRequest);
                        writer.Write(seqNum);
                        writer.Write(corrNum);
                        writer.Write(cancellationTokenId);
                    }

                    stream.Position = 0;
                    await SendAsync(stream, cancellation: default).ConfigureAwait(false);
                }

                try
                {
                    await Task.Delay(delay, cancellation).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
                {
                    return;
                }

                delay += delay;

                if (delay > maxDelay)
                    delay = maxDelay;
            }
        }

        private bool TryGetResultTask<TResult>(int seqNum, out Task<TResult> task)
        {
            var taskCompletionSource = new TaskCompletionSource<TResult>();

            void Callback(MessageType msgType, object value)
            {
                if (msgType == MessageType.ReturnValue)
                {
                    taskCompletionSource.SetResult((TResult)value);
                }
                else
                {

                    if (value is Exception exc)
                    {
                        var preserveStackTrace = typeof(Exception).GetMethod("InternalPreserveStackTrace", BindingFlags.Instance | BindingFlags.NonPublic);
                        if (preserveStackTrace != null)
                            preserveStackTrace.Invoke(exc, null);
                    }
                    else
                    {
                        exc = new Exception();
                    }

                    taskCompletionSource.SetException(exc);
                }
            }

            try
            {
                using (_disposeHelper.GuardDisposal())
                {

                    if (!_responseTable.TryAdd(seqNum, (Callback, typeof(TResult))))
                    {
                        task = default;
                        return false;
                    }

                }
            }
            catch (OperationCanceledException) when (_disposeHelper.IsDisposed)
            {
                throw new ObjectDisposedException(GetType().FullName);
            }

            task = taskCompletionSource.Task;
            return true;
        }

        #endregion

        #region Serialization

        private static void SerializeMethod(BinaryWriter writer, MethodInfo method)
        {
            writer.Write(method.IsGenericMethod);
            writer.Write(method.DeclaringType.AssemblyQualifiedName);
            writer.Write(method.Name);

            var arguments = method.GetParameters();

            writer.Write(arguments.Length);

            foreach (var argument in arguments)
            {
                writer.Write(argument.ParameterType.AssemblyQualifiedName);
            }

            if (method.IsGenericMethod)
            {
                var genericArguments = method.GetGenericArguments();

                writer.Write(genericArguments.Length);

                foreach (var genericArgument in genericArguments)
                {
                    writer.Write(genericArgument.AssemblyQualifiedName);
                }
            }
        }

        private static MethodInfo DeserializeMethod(BinaryReader reader)
        {
            var isGenericMethod = reader.ReadBoolean();

            var declaringType = LoadTypeIgnoringVersion(reader.ReadString());
            var methodName = reader.ReadString();

            var argumentsLengh = reader.ReadInt32();
            var arguments = new Type[argumentsLengh];

            for (var i = 0; i < arguments.Length; i++)
            {
                arguments[i] = LoadTypeIgnoringVersion(reader.ReadString());
            }

            var candidates = declaringType.GetMethods().Where(p => p.Name == methodName);

            if (isGenericMethod)
            {
                var genericArgumentsLength = reader.ReadInt32();
                var genericArguments = new Type[genericArgumentsLength];

                for (var i = 0; i < genericArguments.Length; i++)
                {
                    genericArguments[i] = LoadTypeIgnoringVersion(reader.ReadString());
                }

                candidates = candidates.Where(p => p.IsGenericMethodDefinition && p.GetGenericArguments().Length == genericArgumentsLength)
                                       .Select(p => p.MakeGenericMethod(genericArguments));
            }

            candidates = candidates.Where(p => p.GetParameters().Select(q => q.ParameterType).SequenceEqual(arguments));

            if (candidates.Count() != 1)
            {
                if (candidates.Count() > 1)
                {
                    throw new Exception("Possible method missmatch.");
                }

                throw new Exception("Method not found");
            }

            var result = candidates.First();

            if (result.IsGenericMethodDefinition)
            {
                throw new Exception("Specified method contains unresolved generic arguments");
            }

            return result;
        }

        private void Serialize(BinaryWriter writer, IEnumerable<(object obj, Type objType)> objs, List<CancellationToken> cancellationTokens)
        {
            writer.Write(objs?.Count() ?? 0);

            if (objs != null)
            {
                foreach (var (obj, objType) in objs)
                {
                    Serialize(writer, obj, objType, cancellationTokens);
                }
            }
        }

        private IEnumerable<object> Deserialize(
            BinaryReader reader,
            ParameterInfo[] parameterInfos,
            Dictionary<int, CancellationTokenSource> cancellationTokenSources,
            List<(int id, Type objectType)> activatedProxies)
        {
            var objectCount = reader.ReadInt32();
            for (var i = 0; i < objectCount; i++)
            {
                if (parameterInfos != null && i >= parameterInfos.Length)
                {
                    yield break;// TODO
                }

                yield return Deserialize(reader, parameterInfos?[i].ParameterType, cancellationTokenSources, activatedProxies);
            }
        }

        private void Serialize(BinaryWriter writer, object obj, Type objType, List<CancellationToken> cancellationTokens)
        {
            Debug.Assert(obj == null && (objType.CanContainNull() || objType == typeof(void)) || obj != null && objType.IsAssignableFrom(obj.GetType()));

            switch (obj)
            {
                case null:
                    writer.Write((byte)TypeCode.Null);
                    break;

                case bool value:
                    writer.Write(value ? (byte)TypeCode.True : (byte)TypeCode.False);
                    break;

                case byte value:
                    writer.Write((byte)TypeCode.Byte);
                    writer.Write(value);
                    break;

                case sbyte value:
                    writer.Write((byte)TypeCode.SByte);
                    writer.Write(value);
                    break;

                case short value:
                    writer.Write((byte)TypeCode.Int16);
                    writer.Write(value);
                    break;

                case ushort value:
                    writer.Write((byte)TypeCode.UInt16);
                    writer.Write(value);
                    break;

                case char value:
                    writer.Write((byte)TypeCode.Char);
                    writer.Write(value);
                    break;

                case int value:
                    writer.Write((byte)TypeCode.Int32);
                    writer.Write(value);
                    break;

                case uint value:
                    writer.Write((byte)TypeCode.UInt32);
                    writer.Write(value);
                    break;

                case long value:
                    writer.Write((byte)TypeCode.Int64);
                    writer.Write(value);
                    break;

                case ulong value:
                    writer.Write((byte)TypeCode.UInt64);
                    writer.Write(value);
                    break;

                case float value:
                    writer.Write((byte)TypeCode.Single);
                    writer.Write(value);
                    break;

                case double value:
                    writer.Write((byte)TypeCode.Double);
                    writer.Write(value);
                    break;

                case decimal value:
                    writer.Write((byte)TypeCode.Decimal);
                    writer.Write(value);
                    break;

                case string value:
                    writer.Write((byte)TypeCode.String);
                    writer.Write(value);
                    break;

                case Type type:
                    writer.Write((byte)TypeCode.Type);
                    WriteType(writer, type);
                    break;

                case byte[] value:
                    writer.Write((byte)TypeCode.ByteArray);
                    writer.Write(value.Length);
                    writer.Write(value);
                    break;

                case IProxyInternal proxy:
                    writer.Write((byte)TypeCode.Proxy);
                    SerializeProxy(writer, proxy);
                    break;

                case CancellationToken cancellationToken:
                    writer.Write((byte)TypeCode.CancellationToken);
                    writer.Write(SerializeCancellationToken(cancellationTokens, cancellationToken));

                    break;

                case object _ when objType.IsInterface && !obj.GetType().IsSerializable && LocalProxyRegistered(obj, out var existingProxy):
                    writer.Write((byte)TypeCode.Proxy);
                    SerializeProxy(writer, existingProxy);
                    break;

                default:
                    writer.Write((byte)TypeCode.Other);
                    writer.Flush();
                    GetBinaryFormatter(null, cancellationTokens, new List<(int id, Type objectType)>() { }).Serialize(writer.BaseStream, obj);
                    break;
            }
        }

        private bool LocalProxyRegistered(object obj, out IProxyInternal proxy)
        {
            lock (_proxyLock)
            {
                return _proxyLookup.TryGetValue(obj, out proxy);
            }
        }

        private static int SerializeCancellationToken(List<CancellationToken> cancellationTokens, CancellationToken cancellationToken)
        {
            if (cancellationTokens == null || !cancellationToken.CanBeCanceled)
            {
                return -1;
            }

            cancellationTokens.Add(cancellationToken);
            return cancellationTokens.Count - 1;
        }

        private object Deserialize(
            BinaryReader reader,
            Type expectedType,
            Dictionary<int, CancellationTokenSource> cancellationTokenSources,
            List<(int id, Type objectType)> activatedProxies)
        {
            var typeCode = (TypeCode)reader.ReadByte();

            switch (typeCode)
            {
                case TypeCode.Null:
                    return null;

                case TypeCode.False:
                    return false;

                case TypeCode.True:
                    return true;

                case TypeCode.Byte:
                    return reader.ReadByte();

                case TypeCode.SByte:
                    return reader.ReadSByte();

                case TypeCode.Int16:
                    return reader.ReadInt16();

                case TypeCode.UInt16:
                    return reader.ReadUInt16();

                case TypeCode.Char:
                    return reader.ReadChar();

                case TypeCode.Int32:
                    return reader.ReadInt32();

                case TypeCode.UInt32:
                    return reader.ReadUInt32();

                case TypeCode.Int64:
                    return reader.ReadInt64();

                case TypeCode.UInt64:
                    return reader.ReadUInt64();

                case TypeCode.Single:
                    return reader.ReadSingle();

                case TypeCode.Double:
                    return reader.ReadDouble();

                case TypeCode.Decimal:
                    return reader.ReadDecimal();

                case TypeCode.String:
                    return reader.ReadString();

                case TypeCode.Type:
                    return ReadType(reader);

                case TypeCode.ByteArray:
                    var length = reader.ReadInt32();
                    return reader.ReadBytes(length);

                case TypeCode.Proxy:
                    return DeserializeProxy(reader, expectedType, activatedProxies);

                case TypeCode.CancellationToken:
                    var id = reader.ReadInt32();
                    return DeserializeCancellationToken(id, cancellationTokenSources);

                case TypeCode.Other:
                    return GetBinaryFormatter(cancellationTokenSources, null, activatedProxies).Deserialize(reader.BaseStream);

                default:
                    throw new FormatException("Unknown type code.");
            }
        }

        private static object DeserializeCancellationToken(int id, Dictionary<int, CancellationTokenSource> cancellationTokenSources)
        {
            if (id < 0 || cancellationTokenSources == null)
            {
                return CancellationToken.None;
            }

            var cancellationToken = cancellationTokenSources.GetOrAdd(id, _ => new CancellationTokenSource()).Token;
            return cancellationToken;
        }

        private void SerializeProxy(BinaryWriter writer, IProxyInternal proxy)
        {
            if (proxy.LocalInstance != null)
            {
                proxy = RegisterLocalProxy(proxy);

                writer.Write((byte)ProxyOwner.Remote); // We own the proxy, but for the remote end, this is a remote proxy.
            }
            else
            {
                writer.Write((byte)ProxyOwner.Local);
                var isActivated = proxy.IsActivated;

                writer.Write(isActivated);

                if (!isActivated)
                {
                    writer.Write((byte)proxy.ActivationMode);
                    Serialize(writer, proxy.ActivationParamers?.Select(p => (p, p?.GetType())), null);
                }
            }

            var proxyType = proxy.RemoteType;
            writer.Write(proxyType.AssemblyQualifiedName);
            writer.Write((proxy.LocalInstance?.GetType() ?? proxyType).AssemblyQualifiedName);
            writer.Write(proxy.Id);
        }

        private object DeserializeProxy(BinaryReader reader, Type expectedType, List<(int id, Type objectType)> activatedProxies)
        {
            var proxyOwner = (ProxyOwner)reader.ReadByte();

            ActivationMode mode = default;
            object[] parameters = null;

            if (proxyOwner == ProxyOwner.Local)
            {
                var isActivated = reader.ReadBoolean();

                if (!isActivated)
                {
                    mode = (ActivationMode)reader.ReadByte();
                    parameters = Deserialize(reader, (ParameterInfo[])null, null, activatedProxies).ToArray();
                }
            }

            var proxyType = LoadTypeIgnoringVersion(reader.ReadString());
            var actualType = LoadTypeIgnoringVersion(reader.ReadString());
            var proxyId = reader.ReadInt32();

            return DeserializeProxy(expectedType, proxyOwner, proxyType, actualType, proxyId, mode, parameters, activatedProxies);
        }

        private object DeserializeProxy(
            Type expectedType,
            ProxyOwner proxyOwner,
            Type proxyType,
            Type actualType,
            int proxyId,
            ActivationMode mode,
            object[] parameters,
            List<(int id, Type objectType)> activatedProxies)
        {
            if (proxyOwner == ProxyOwner.Remote)
            {
                IProxyInternal proxy;

                lock (_remoteProxiesMutex)
                {
                    if (!_remoteProxies.TryGetValue(proxyId, out proxy))
                    {
                        proxy = null;
                    }
                }

                if (proxy == null)
                {
                    var type = typeof(Proxy<>).MakeGenericType(proxyType);
                    proxy = (IProxyInternal)Activator.CreateInstance(type, BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public, null, new object[]
                    {
                            this,
                            proxyId,
                            actualType
                    }, null);
                }

                try
                {
                    using (_disposeHelper.GuardDisposal())
                    {
                        lock (_remoteProxiesMutex)
                        {
                            if (_remoteProxies.TryGetValue(proxy.Id, out var p))
                            {
                                proxy = p;
                            }
                            else
                            {
                                _remoteProxies.Add(proxy.Id, proxy);
                            }
                        }
                    }
                }
                catch (OperationCanceledException) when (_disposeHelper.IsDisposed)
                {
                    throw new ObjectDisposedException(GetType().FullName);
                }

                if (expectedType == null || expectedType.GetInterfaces().Contains(typeof(IProxy)))
                {
                    return proxy;
                }

                return CreateTransparentProxy(expectedType, proxy);
            }
            else
            {
                if (!TryGetProxyById(proxyId, out var proxy))
                {
                    object instance;
                    var ownsInstance = false;

                    if (mode == ActivationMode.Create)
                    {
                        instance = ActivatorUtilities.CreateInstance(_serviceProvider, proxyType, parameters);
                        ownsInstance = true;
                    }
                    else // if (mode == ActivationMode.Load)
                    {
                        instance = _serviceProvider.GetRequiredService(proxyType);
                    }

                    proxy = (IProxyInternal)Activator.CreateInstance(typeof(Proxy<>).MakeGenericType(proxyType), BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public, null, new[] { instance, ownsInstance }, null);
                    proxy = RegisterLocalProxy(proxy, proxyId);

                    activatedProxies.Add((proxyId, proxy.LocalInstance.GetType()));
                }

                if (expectedType != null && expectedType.IsInstanceOfType(proxy.LocalInstance))
                    return proxy.LocalInstance;

                return proxy;
            }
        }

        private static IProxyInternal CreateTransparentProxy(Type expectedType, IProxyInternal proxy)
        {
            var transparentProxyTypeDefinition = typeof(TransparentProxy<>);
            var transparentProxyType = transparentProxyTypeDefinition.MakeGenericType(expectedType);

            var createMethod = transparentProxyType.GetMethod(
                nameof(TransparentProxy<object>.Create),
                BindingFlags.Static | BindingFlags.NonPublic,
                Type.DefaultBinder,
                new[] { typeof(IProxyInternal) },
                modifiers: null);

            Debug.Assert(createMethod.ReturnType == expectedType);

            return (IProxyInternal)createMethod.Invoke(obj: null, new[] { proxy });
        }

        private static void WriteType(BinaryWriter writer, Type type)
        {
            writer.Write(type.AssemblyQualifiedName);
        }

        private static Type ReadType(BinaryReader reader)
        {
            var assemblyQualifiedName = reader.ReadString();
            return LoadTypeIgnoringVersion(assemblyQualifiedName);
        }

        private BinaryFormatter GetBinaryFormatter(
                Dictionary<int, CancellationTokenSource> cancellationTokenSources,
                List<CancellationToken> cancellationTokens,
                List<(int id, Type objectType)> activatedProxies)
        {
            var selector = new FallbackSurrogateSelector(this, activatedProxies);

            HashSet<Type> transparentProxyTypes;

            lock (_loadedTransparentProxyTypesMutex)
            {
                transparentProxyTypes = _loadedTransparentProxyTypes.ToHashSet();
            }

            var proxySurrogate = new ProxySurrogate(this, activatedProxies);
            foreach (var remoteType in _loadedRemoteTypes) // Volatile read op.
            {
                selector.AddSurrogate(typeof(Proxy<>).MakeGenericType(remoteType), new StreamingContext(), proxySurrogate);

                var interfaces = remoteType.GetInterfaces();

                foreach (var @interface in interfaces)
                {
                    transparentProxyTypes.Add(@interface);
                }
            }

            foreach (var transparentProxyType in transparentProxyTypes) // Volatile read op.
            {
                selector.AddSurrogate(transparentProxyType, new StreamingContext(), proxySurrogate);
            }

            var cancellationTokenSurrogate = new CancellationTokenSurrogate(cancellationTokenSources, cancellationTokens);

            selector.AddSurrogate(typeof(CancellationToken), new StreamingContext(), cancellationTokenSurrogate);

            return new BinaryFormatter(selector, context: default) { AssemblyFormat = FormatterAssemblyStyle.Simple };
        }

        private static Type LoadTypeIgnoringVersion(string assemblyQualifiedName)
        {
            return Type.GetType(assemblyQualifiedName, assemblyName => { assemblyName.Version = null; return Assembly.Load(assemblyName); }, null);
        }

        private enum TypeCode : byte
        {
            Other,
            Null,
            False,
            True,
            Byte,
            SByte,
            Int16,
            UInt16,
            Char,
            Int32,
            UInt32,
            Int64,
            UInt64,
            Single,
            Double,
            Decimal,
            String,
            Type,
            ByteArray,
            CancellationToken,
            Proxy
        }

        private enum ProxyOwner : byte
        {
            Local,
            Remote
        }

        #endregion

        private enum MessageType : byte
        {
            MethodCall,
            ReturnValue,
            ReturnException,
            Activation,
            Deactivation,
            CancellationRequest
        }

        private sealed class ProxySurrogate : ISerializationSurrogate
        {
            private readonly ProxyHost _proxyHost;
            private readonly List<(int id, Type objectType)> _activatedProxies;

            public ProxySurrogate(ProxyHost proxyHost, List<(int id, Type objectType)> activatedProxies)
            {
                Debug.Assert(proxyHost != null);
                Debug.Assert(activatedProxies != null);
                _proxyHost = proxyHost;
                _activatedProxies = activatedProxies;
            }

            public void GetObjectData(object obj, SerializationInfo info, StreamingContext context)
            {
                if (!(obj is IProxyInternal proxy) && !_proxyHost.LocalProxyRegistered(obj, out proxy))
                {
                    throw new SerializationException("Type " + obj.GetType() + " cannot be serialized.");
                }

                var remoteType = proxy.RemoteType;
                byte proxyOwner;

                if (proxy.LocalInstance != null)
                {
                    proxy = _proxyHost.RegisterLocalProxy(proxy);

                    proxyOwner = (byte)ProxyOwner.Remote; // We own the proxy, but for the remote end, this is a remote proxy.
                }
                else
                {
                    proxyOwner = (byte)ProxyOwner.Local;

                    info.AddValue("isActivated", proxy.IsActivated);

                    if (!proxy.IsActivated)
                    {
                        info.AddValue("mode", (byte)proxy.ActivationMode);
                        info.AddValue("parameters", proxy.ActivationParamers);
                    }
                }

                var expectedType = obj is IProxyInternal ? typeof(Proxy<>).MakeGenericType(remoteType) : remoteType;

                for (var current = obj.GetType(); current != typeof(object); current = current.BaseType)
                {
                    if (current.IsGenericType && current.GetGenericTypeDefinition() == typeof(TransparentProxy<>))
                    {
                        expectedType = current.GetGenericArguments()[0];
                    }
                }

                info.AddValue("proxyOwner", proxyOwner);
                info.AddValue("id", proxy.Id);
                info.AddValue("objectType", (proxy.LocalInstance?.GetType() ?? remoteType).AssemblyQualifiedName);
                info.AddValue("expectedType", expectedType.AssemblyQualifiedName);
                info.SetType(typeof(Proxy<>).MakeGenericType(remoteType));
            }

            public object SetObjectData(object obj, SerializationInfo info, StreamingContext context, ISurrogateSelector selector)
            {
                var remoteType = info.ObjectType.GetGenericArguments()[0];
                var proxyOwner = (ProxyOwner)info.GetByte("proxyOwner");

                ActivationMode mode = default;
                object[] parameters = null;


                if (proxyOwner == ProxyOwner.Local)
                {
                    mode = (ActivationMode)info.GetByte("mode");
                    parameters = (object[])info.GetValue("parameters", typeof(object[]));
                }

                var id = info.GetInt32("id");
                var objectType = LoadTypeIgnoringVersion(info.GetString("objectType"));
                var expectedType = LoadTypeIgnoringVersion(info.GetString("expectedType"));

                return _proxyHost.DeserializeProxy(expectedType, proxyOwner, remoteType, objectType, id, mode, parameters, _activatedProxies);
            }
        }

        private sealed class CancellationTokenSurrogate : ISerializationSurrogate
        {
            private readonly Dictionary<int, CancellationTokenSource> _cancellationTokenSources;
            private readonly List<CancellationToken> _cancellationTokens;

            public CancellationTokenSurrogate(
                Dictionary<int, CancellationTokenSource> cancellationTokenSources,
                List<CancellationToken> cancellationTokens)
            {
                _cancellationTokenSources = cancellationTokenSources;
                _cancellationTokens = cancellationTokens;
            }

            public void GetObjectData(object obj, SerializationInfo info, StreamingContext context)
            {
                Debug.Assert(obj is CancellationToken);
                var id = SerializeCancellationToken(_cancellationTokens, (CancellationToken)obj);
                info.AddValue("id", id);
            }

            public object SetObjectData(object obj, SerializationInfo info, StreamingContext context, ISurrogateSelector selector)
            {
                var id = info.GetInt32("id");
                return DeserializeCancellationToken(id, _cancellationTokenSources);
            }
        }

        private sealed class FallbackSurrogateSelector : SurrogateSelector
        {
            private readonly ProxyHost _proxyHost;
            private readonly List<(int id, Type objectType)> _activatedProxies;

            public FallbackSurrogateSelector(ProxyHost proxyHost, List<(int id, Type objectType)> activatedProxies)
            {
                Debug.Assert(proxyHost != null);
                Debug.Assert(activatedProxies != null);
                _proxyHost = proxyHost;
                _activatedProxies = activatedProxies;
            }

            public override ISerializationSurrogate GetSurrogate(Type type, StreamingContext context, out ISurrogateSelector selector)
            {
                var surrogate = base.GetSurrogate(type, context, out selector);

                if (surrogate == null && !type.IsSerializable)
                {
                    return new ProxySurrogate(_proxyHost, _activatedProxies);
                }

                return surrogate;
            }
        }
    }

    internal enum ActivationMode : byte
    {
        Create,
        Load
    }
}

#nullable enable
