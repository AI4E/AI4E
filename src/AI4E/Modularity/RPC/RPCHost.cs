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
using Nito.AsyncEx;

namespace AI4E.Modularity.RPC
{
    public sealed class RPCHost : IDisposable
    {
        private readonly Stream _stream;
        private readonly AsyncLock _sendLock = new AsyncLock();
        private readonly IAsyncProcess _receiveProcess;
        private readonly ConcurrentDictionary<int, Action<MessageType, object>> _responseTable = new ConcurrentDictionary<int, Action<MessageType, object>>();
        private readonly object _proxyLock = new object();
        private readonly Dictionary<object, IProxy> _proxyLookup = new Dictionary<object, IProxy>();
        private readonly Dictionary<int, IProxy> _proxies = new Dictionary<int, IProxy>();
        private volatile int _isDisposed = 0;
        private int _nextSeqNum = 0;
        private int _nextProxyId = 0;

        private bool IsDisposed => _isDisposed != 0; // Volatile read op.

        public RPCHost(Stream stream)
        {
            if (stream == null)
                throw new ArgumentNullException(nameof(stream));

            _stream = stream;
            _receiveProcess = new AsyncProcess(ReceiveProcess);
            _receiveProcess.Start();
        }

        public async Task<Proxy<TRemote>> ActivateAsync<TRemote>(CancellationToken cancellation)
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
                writer.Write((byte)MessageType.Activation);
                writer.Write(seqNum);
                Serialize(writer, proxyId);
            }

            return SendAsync(message, cancellation);
        }

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _isDisposed, 1) != 0)
                return;

            _receiveProcess.Terminate();
        }

        private async Task ReceiveProcess(CancellationToken cancellation)
        {
            while (cancellation.ThrowOrContinue())
            {
                try
                {
                    var message = new Message();
                    await message.ReadAsync(_stream, cancellation);

                    HandleMessageAsync(message, cancellation).HandleExceptions();
                }
                catch (OperationCanceledException) when (cancellation.IsCancellationRequested) { throw; }
                catch (Exception exc)
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
                var type = (Type)Deserialize(reader, expectedType: default);
                var instance = Activator.CreateInstance(type, true);
                var proxy = (IProxy)Activator.CreateInstance(typeof(Proxy<>).MakeGenericType(type), BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public, null, new[] { instance }, null);
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

        private IProxy RegisterLocalProxy(IProxy proxy)
        {
            lock (_proxyLock)
            {
                if (_proxyLookup.TryGetValue(proxy.LocalInstance, out var existing))
                {
                    return existing;
                }

                var id = Interlocked.Increment(ref _nextProxyId);

                proxy.Register(this, id);

                _proxyLookup.Add(proxy.LocalInstance, proxy);
                _proxies.Add(id, proxy);
            }

            return proxy;
        }

        private void UnregisterLocalProxy(IProxy proxy)
        {
            lock (_proxyLock)
            {
                var id = proxy.Id;

                if (id == 0)
                {
                    _proxyLookup.TryGetValue(proxy.LocalInstance, out proxy);
                    id = proxy.Id;
                }

                _proxyLookup.Remove(proxy.LocalInstance);
                _proxies.Remove(id);
            }
        }

        private bool TryGetProxyById(int proxyId, out IProxy proxy)
        {
            lock (_proxyLock)
            {
                return _proxies.TryGetValue(proxyId, out proxy);
            }
        }

        private async Task ReceiveMethodCallAsync(BinaryReader reader, int seqNum, CancellationToken cancellation)
        {
            var result = default(object);
            var exception = default(Exception);
            var waitTask = false;

            try
            {
                var proxyId = reader.ReadInt32();
                waitTask = reader.ReadBoolean();
                var method = DeserializeMethod(reader);
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

        private void ReceiveResult(MessageType messageType, BinaryReader reader)
        {
            var corr = reader.ReadInt32();
            var value = Deserialize(reader, expectedType: default);

            if (_responseTable.TryRemove(corr, out var callback))
            {
                callback(messageType, value);
            }
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

        private static object GetExpressionValue(Expression expression)
        {
            if (expression is ConstantExpression constant)
            {
                return constant.Value;
            }

            if (expression is MemberExpression memberExpression)
            {
                if (memberExpression.Member is FieldInfo field && memberExpression.Expression is ConstantExpression fieldOwner)
                {
                    return field.GetValue(fieldOwner.Value);
                }

                // TODO
            }

            var valueFactory = Expression.Lambda<Func<object>>(Expression.Convert(expression, typeof(object))).Compile();

            return valueFactory();
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

        private async Task SendAsync(Message message, CancellationToken cancellation)
        {
            using (await _sendLock.LockAsync())
            {
                await message.WriteAsync(_stream, cancellation);
            }
        }

        private enum MessageType : byte
        {
            MethodCall,
            ReturnValue,
            ReturnException,
            Activation,
            Deactivation
        }

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

            var candidates = declaringType.GetMethods().Where(p => p.Name == methodName).Where(p => p.GetParameters().Select(q => q.ParameterType).SequenceEqual(arguments));

            if (isGenericMethod)
            {
                var genericArgumentsLength = reader.ReadInt32();
                var genericArguments = new Type[genericArgumentsLength];

                for (var i = 0; i < genericArguments.Length; i++)
                {
                    genericArguments[i] = LoadTypeIgnoringVersion(reader.ReadString());
                }

                candidates = candidates.Where(p => p.IsGenericMethodDefinition && p.GetGenericArguments().Length == genericArgumentsLength);

                if (candidates.Count() != 1)
                {
                    if (candidates.Count() > 1)
                    {
                        throw new Exception("Possible method missmatch.");
                    }

                    throw new Exception("Method not found");
                }

                return candidates.First().MakeGenericMethod(genericArguments);
            }

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

                    if (!TryGetProxyById(proxyId, out var proxy))
                    {
                        throw new Exception("Proxy not found.");
                    }

                    if (expectedType != null && expectedType.IsInstanceOfType(proxy.LocalInstance))
                        return proxy.LocalInstance;

                    return proxy;

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
            Proxy
        }

        private enum ProxyOwner : byte
        {
            Local,
            Remote
        }

        #endregion
    }
}
