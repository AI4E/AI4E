/* Summary
 * --------------------------------------------------------------------------------------------------------------------
 * Filename:        RPCHost.cs
 * Types:           (1) AI4E.Modularity.RPC.RPCHost
 *                  (2) AI4E.Modularity.RPC.ActivationMode
 *                  (3) AI4E.Modularity.RPC.RPCHost.MessageType
 *                  (4) AI4E.Modularity.RPC.RPCHost.ProxyOwner
 *                  (5) AI4E.Modularity.RPC.RPCHost.TypeCode
 * Version:         1.0
 * Author:          Andreas Trütschel
 * Last modified:   18.03.2018 
 * --------------------------------------------------------------------------------------------------------------------
 */

/* License
 * --------------------------------------------------------------------------------------------------------------------
 * This file is part of the AI4E distribution.
 *   (https://github.com/AI4E/AI4E)
 * Copyright (c) 2018 Andreas Truetschel and contributors.
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

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using AI4E.Async;
using AI4E.Processing;
using Microsoft.Extensions.DependencyInjection;
using Nito.AsyncEx;

namespace AI4E.Modularity.RPC
{
    public sealed class RPCHost : IAsyncInitialization, IAsyncDisposable
    {
        #region Fields

        private readonly Stream _stream;
        private readonly IServiceProvider _serviceProvider;
        private readonly AsyncLock _sendLock = new AsyncLock();
        private readonly IAsyncProcess _receiveProcess;
        private readonly ConcurrentDictionary<int, Action<MessageType, object>> _responseTable = new ConcurrentDictionary<int, Action<MessageType, object>>();
        private readonly Dictionary<object, IProxy> _proxyLookup = new Dictionary<object, IProxy>();
        private readonly Dictionary<int, IProxy> _proxies = new Dictionary<int, IProxy>();
        private readonly object _proxyLock = new object();
        private readonly AsyncDisposeHelper _disposeHelper;
        private readonly AsyncInitializationHelper _initializationHelper;
        private int _nextSeqNum = 0;
        private int _nextProxyId = 0;

        #endregion

        #region Ctor

        public RPCHost(Stream stream, IServiceProvider serviceProvider)
        {
            if (stream == null)
                throw new ArgumentNullException(nameof(stream));

            if (serviceProvider == null)
                throw new ArgumentNullException(nameof(serviceProvider));

            _stream = stream;
            _serviceProvider = serviceProvider;

            _receiveProcess = new AsyncProcess(ReceiveProcess);
            _initializationHelper = new AsyncInitializationHelper(InitializeInternalAsync);
            _disposeHelper = new AsyncDisposeHelper(DisposeInternalAsync);
        }

        #endregion

        #region Activation

        public async Task<Proxy<TRemote>> ActivateAsync<TRemote>(ActivationMode mode, CancellationToken cancellation)
            where TRemote : class
        {
            int seqNum;
            Task<Proxy<TRemote>> result;
            Message message;

            do
            {
                seqNum = Interlocked.Increment(ref _nextSeqNum);
                message = new Message();

                using (var stream = message.PushFrame().OpenStream())
                using (var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: false))
                {
                    writer.Write((byte)MessageType.Activation);
                    writer.Write(seqNum);
                    writer.Write((byte)mode);
                    Serialize(writer, typeof(TRemote));
                }
            }
            while (!TryGetResultTask(seqNum, out result));

            await SendAsync(message, cancellation);

            return await result;
        }

        internal Task Deactivate(int proxyId, CancellationToken cancellation)
        {
            var seqNum = Interlocked.Increment(ref _nextSeqNum);
            var message = new Message();

            using (var stream = message.PushFrame().OpenStream())
            using (var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: false))
            {
                writer.Write((byte)MessageType.Deactivation);
                writer.Write(seqNum);
                writer.Write(proxyId);
            }

            return SendAsync(message, cancellation);
        }

        #endregion

        #region Initialization

        public Task Initialization => _initializationHelper.Initialization;

        private async Task InitializeInternalAsync(CancellationToken cancellation)
        {
            await _receiveProcess.StartAsync(cancellation);
        }

        #endregion

        #region Disposal

        public Task Disposal => _disposeHelper.Disposal;

        public void Dispose()
        {
            _disposeHelper.Dispose();
        }

        public Task DisposeAsync()
        {
            return _disposeHelper.DisposeAsync();
        }

        private async Task DisposeInternalAsync()
        {
            await _initializationHelper.CancelAsync();

            await _receiveProcess.TerminateAsync();

            lock (_proxyLock)
            {
                foreach (var proxy in _proxies.Values.ToList())
                {
                    proxy.Dispose();
                }
            }
        }

        #endregion

        #region Proxies

        private IProxy RegisterLocalProxy(IProxy proxy)
        {
            lock (_proxyLock)
            {
                if (_proxyLookup.TryGetValue(proxy.LocalInstance, out var existing))
                {
                    return existing;
                }

                var id = Interlocked.Increment(ref _nextProxyId);

                proxy.Register(this, id, () => UnregisterLocalProxy(proxy));

                _proxyLookup.Add(proxy.LocalInstance, proxy);
                _proxies.Add(id, proxy);
            }

            return proxy;
        }

        private void UnregisterLocalProxy(IProxy proxy)
        {
            lock (_proxyLock)
            {
                _proxyLookup.Remove(proxy.LocalInstance);
                _proxies.Remove(proxy.Id);
            }
        }

        private bool TryGetProxyById(int proxyId, out IProxy proxy)
        {
            lock (_proxyLock)
            {
                return _proxies.TryGetValue(proxyId, out proxy);
            }
        }

        #endregion

        #region Receive

        private async Task ReceiveProcess(CancellationToken cancellation)
        {
            while (cancellation.ThrowOrContinue())
            {
                try
                {
                    var message = new Message();
                    try
                    {
                        await message.ReadAsync(_stream, cancellation);
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

                    // We do not want the process to be disturbed/blocked/deadlocked
                    Task.Run(() => HandleMessageAsync(message, cancellation)).HandleExceptions();
                }
                catch (OperationCanceledException) when (cancellation.IsCancellationRequested) { throw; }
                catch (Exception)
                {
                    // TODO: Log exception
                }
            }
        }

        private async Task HandleMessageAsync(Message message, CancellationToken cancellation)
        {
            using (var stream = message.PopFrame().OpenStream())
            using (var reader = new BinaryReader(stream, Encoding.UTF8, leaveOpen: false))
            {
                var messageType = (MessageType)reader.ReadByte();
                var seqNum = reader.ReadInt32();

                switch (messageType)
                {
                    case MessageType.ReturnValue:
                    case MessageType.ReturnException:
                        ReceiveResult(messageType, reader);
                        break;

                    case MessageType.MethodCall:
                        await ReceiveMethodCallAsync(reader, seqNum, cancellation);
                        break;

                    case MessageType.Activation:
                        await ReceiveActivationAsync(reader, seqNum, cancellation);
                        break;

                    case MessageType.Deactivation:
                        ReceiveDeactivation(reader);
                        break;
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

            try
            {
                var mode = (ActivationMode)reader.ReadByte();
                var type = (Type)Deserialize(reader, expectedType: default);

                var instance = default(object);
                var ownsInstance = false;

                if (mode == ActivationMode.Create)
                {
                    instance = ActivatorUtilities.CreateInstance(_serviceProvider, type);
                    ownsInstance = true;
                }
                else if (mode == ActivationMode.LoadFromServices)
                {
                    instance = _serviceProvider.GetRequiredService(type);
                }

                var proxy = (IProxy)Activator.CreateInstance(typeof(Proxy<>).MakeGenericType(type), BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public, null, new[] { instance, ownsInstance }, null);
                result = RegisterLocalProxy(proxy);
            }
            catch (TargetInvocationException exc)
            {
                exception = exc.InnerException;
            }
            catch (Exception exc)
            {
                exception = exc;
            }

            await SendResult(seqNum, result, exception, waitTask: false, cancellation);
        }

        private async Task ReceiveMethodCallAsync(BinaryReader reader, int seqNum, CancellationToken cancellation)
        {
            var result = default(object);
            var exception = default(Exception);
            var waitTask = false;

            MethodInfo method;

            try
            {
                var proxyId = reader.ReadInt32();
                waitTask = reader.ReadBoolean();
                method = DeserializeMethod(reader);
                var arguments = Deserialize(reader, method.GetParameters()).ToArray();

                if (!TryGetProxyById(proxyId, out var proxy))
                {
                    throw new Exception("Proxy not found."); // TODO
                }

                var instance = proxy.LocalInstance;

                if (instance == null)
                {
                    throw new Exception("Proxy not found."); // TODO
                }

                result = method.Invoke(instance, arguments);
            }
            catch (TargetInvocationException exc)
            {
                exception = exc.InnerException;
            }
            catch (Exception exc)
            {
                exception = exc;
            }

            await SendResult(seqNum, result, exception, waitTask, cancellation);
        }

        private void ReceiveResult(MessageType messageType, BinaryReader reader)
        {
            var corr = reader.ReadInt32();
            var value = Deserialize(reader, expectedType: default);

            if (_responseTable.TryRemove(corr, out var callback))
            {
                callback(messageType, value);
            }
        }

        #endregion

        #region Send

        private async Task SendAsync(Message message, CancellationToken cancellation)
        {
            using (await _sendLock.LockAsync())
            {
                await message.WriteAsync(_stream, cancellation);
            }
        }

        private async Task SendResult(int corrNum, object result, Exception exception, bool waitTask, CancellationToken cancellation)
        {
            if (exception == null && waitTask)
            {
                try
                {
                    var t = (Task)result;
                    await t;
                    result = t.GetResult();
                }
                catch (Exception exc)
                {
                    exception = exc;
                }
            }

            var message = new Message();
            var seqNum = Interlocked.Increment(ref _nextSeqNum);

            using (var stream = message.PushFrame().OpenStream())
            using (var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: false))
            {
                writer.Write(exception == null ? (byte)MessageType.ReturnValue : (byte)MessageType.ReturnException);
                writer.Write(seqNum);
                writer.Write(corrNum);
                Serialize(writer, exception ?? result);
            }

            await SendAsync(message, cancellation);
        }

        internal async Task<TResult> SendMethodCallAsync<TResult>(Expression expression, int proxyId, bool waitTask)
        {
            if (expression == null)
                throw new ArgumentNullException(nameof(expression));

            var message = new Message();
            var seqNum = default(int);
            var task = default(Task<TResult>);

            do
            {
                seqNum = Interlocked.Increment(ref _nextSeqNum);

                using (var stream = message.PushFrame().OpenStream())
                using (var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: false))
                {
                    writer.Write((byte)MessageType.MethodCall);
                    writer.Write(seqNum);

                    var method = default(MethodInfo);
                    var arguments = Enumerable.Empty<Expression>();
                    if (expression is MethodCallExpression methodCallExpression)
                    {
                        method = methodCallExpression.Method;
                        arguments = methodCallExpression.Arguments;
                    }
                    else if (expression is MemberExpression memberExpression && memberExpression.Member is PropertyInfo property)
                    {
                        method = property.GetGetMethod();
                    }
                    else
                    {
                        throw new InvalidOperationException(); // TODO: What about Property writes? What about indexed properties?
                    }

                    writer.Write(proxyId);
                    writer.Write(waitTask);
                    SerializeMethod(writer, method);
                    Serialize(writer, arguments.Select(p => GetExpressionValue(p)));
                    writer.Flush();
                }
            }
            while (!TryGetResultTask(seqNum, out task));

            await SendAsync(message, cancellation: default);
            return await task;
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
                    var exc = value as Exception;

                    if (exc == null)
                    {
                        exc = new Exception();
                    }
                    else
                    {
                        var preserveStackTrace = typeof(Exception).GetMethod("InternalPreserveStackTrace", BindingFlags.Instance | BindingFlags.NonPublic);
                        if (preserveStackTrace != null)
                            preserveStackTrace.Invoke(exc, null);
                    }

                    taskCompletionSource.SetException(exc);
                }
            }

            if (!_responseTable.TryAdd(seqNum, Callback))
            {
                task = default;
                return false;
            }

            task = taskCompletionSource.Task;
            return true;
        }

        #endregion

        #region Serialization

        private void SerializeMethod(BinaryWriter writer, MethodInfo method)
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

        private MethodInfo DeserializeMethod(BinaryReader reader)
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

        private void Serialize(BinaryWriter writer, IEnumerable<object> objs)
        {
            writer.Write(objs.Count());
            foreach (var obj in objs)
            {
                Serialize(writer, obj);
            }
        }

        private IEnumerable<object> Deserialize(BinaryReader reader, ParameterInfo[] parameterInfos)
        {
            var objectCount = reader.ReadInt32();
            for (var i = 0; i < objectCount; i++)
            {
                if (i >= parameterInfos.Length)
                {
                    yield break;// TODO
                }

                yield return Deserialize(reader, parameterInfos[i].ParameterType);
            }
        }

        private void Serialize(BinaryWriter writer, object obj)
        {
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
                    writer.Write(type.AssemblyQualifiedName);
                    break;

                case byte[] value:
                    writer.Write((byte)TypeCode.ByteArray);
                    writer.Write(value.Length);
                    writer.Write(value);
                    break;

                case IProxy proxy:
                    writer.Write((byte)TypeCode.Proxy);

                    if (proxy.LocalInstance != null)
                    {
                        proxy = RegisterLocalProxy(proxy);

                        writer.Write((byte)ProxyOwner.Remote); // We own the proxy, but for the remote end, this is a remote proxy.
                    }
                    else
                    {
                        writer.Write((byte)ProxyOwner.Local);
                    }

                    var proxyType = proxy.GetType().GetGenericArguments()[0];
                    writer.Write(proxyType.AssemblyQualifiedName);
                    writer.Write((proxy?.LocalInstance.GetType() ?? proxyType).AssemblyQualifiedName);
                    writer.Write(proxy.Id);
                    break;

                case CancellationToken cancellationToken:
                    writer.Write((byte)TypeCode.CancellationToken);
                    break;

                default:
                    writer.Write((byte)TypeCode.Other);
                    writer.Flush();
                    new BinaryFormatter().Serialize(writer.BaseStream, obj);
                    break;
            }
        }

        private object Deserialize(BinaryReader reader, Type expectedType)
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
                    var assemblyQualifiedName = reader.ReadString();
                    return LoadTypeIgnoringVersion(assemblyQualifiedName);

                case TypeCode.ByteArray:
                    var length = reader.ReadInt32();
                    return reader.ReadBytes(length);

                case TypeCode.Proxy:
                    var proxyOwner = (ProxyOwner)reader.ReadByte();
                    var proxyType = LoadTypeIgnoringVersion(reader.ReadString());
                    var actualType = LoadTypeIgnoringVersion(reader.ReadString());
                    var proxyId = reader.ReadInt32();

                    if (proxyOwner == ProxyOwner.Remote)
                    {
                        var type = typeof(Proxy<>).MakeGenericType(proxyType);
                        return Activator.CreateInstance(type, BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public, null, new object[]
                        {
                            this,
                            proxyId,
                            actualType
                        }, null);
                    }
                    else
                    {
                        if (!TryGetProxyById(proxyId, out var proxy))
                        {
                            throw new Exception("Proxy not found.");
                        }

                        if (expectedType != null && expectedType.IsInstanceOfType(proxy.LocalInstance))
                            return proxy.LocalInstance;

                        return proxy;
                    }

                case TypeCode.CancellationToken:
                    return CancellationToken.None; // TODO: Cancellation token support

                case TypeCode.Other:
                    return new BinaryFormatter().Deserialize(reader.BaseStream);

                default:
                    throw new FormatException("Unknown type code.");
            }
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

        private static object GetExpressionValue(Expression expression)
        {
            if (expression is ConstantExpression constant)
            {
                return constant.Value;
            }

            if (expression is MemberExpression memberExpression)
            {
                if (memberExpression.Member is FieldInfo field &&
                    memberExpression.Expression is ConstantExpression fieldOwner)
                {
                    return field.GetValue(fieldOwner.Value);
                }

                // TODO
            }

            var valueFactory = Expression.Lambda<Func<object>>(Expression.Convert(expression, typeof(object))).Compile();

            return valueFactory();
        }

        private enum MessageType : byte
        {
            MethodCall,
            ReturnValue,
            ReturnException,
            Activation,
            Deactivation
        }
    }

    public enum ActivationMode : byte
    {
        Create,
        LoadFromServices
    }
}
