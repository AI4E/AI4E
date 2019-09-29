using System;
using System.Linq.Expressions;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using AI4E.Utils;
using static System.Diagnostics.Debug;

/*
 * This is a shim to specify that a message is published locally only, whether it is an in-memory or remote dispatcher.
 * This breaks the abstraction. Can we do any better in providing this functionality?
 * A possible solution is to add a DispatchLocalMethod to the IMessageDispatcher interface that behaves exactly the some for in-memory and remote dispatchers,
 * 
 */

namespace AI4E.Storage.Domain
{
    public static class MessageDispatcherExtension
    {
        private static readonly Func<IMessageDispatcher, bool> _isRemoteMessageDispatcher;
        private static readonly Func<IMessageDispatcher, DispatchDataDictionary, bool, CancellationToken, ValueTask<IDispatchResult>> _dispatchLocalAsync;

        static MessageDispatcherExtension()
        {
            if (!TypeLoadHelper.TryLoadTypeFromUnqualifiedName("AI4E.Routing.IRemoteMessageDispatcher", out var remoteMessageDispatcherType))
            {
                _isRemoteMessageDispatcher = _ => false;
                _dispatchLocalAsync = null;
                return;
            }

            var dispatchLocalAsyncMethod = remoteMessageDispatcherType.GetMethod(
                    "DispatchLocalAsync",
                    BindingFlags.Public | BindingFlags.Instance,
                    Type.DefaultBinder,
                    new Type[] { typeof(DispatchDataDictionary), typeof(bool), typeof(CancellationToken) },
                    modifiers: null);

            Assert(dispatchLocalAsyncMethod != null);
            Assert(dispatchLocalAsyncMethod.ReturnType == typeof(ValueTask<IDispatchResult>));

            var messageDispatcherParameter = Expression.Parameter(typeof(IMessageDispatcher), "messageDispatcher");
            var isRemoteMessageDispatcher = Expression.TypeIs(messageDispatcherParameter, remoteMessageDispatcherType);

            _isRemoteMessageDispatcher = Expression.Lambda<Func<IMessageDispatcher, bool>>(isRemoteMessageDispatcher, messageDispatcherParameter).Compile();

            var dispatchDataParameter = Expression.Parameter(typeof(DispatchDataDictionary), "dispatchData");
            var publishParameter = Expression.Parameter(typeof(bool), "publish");
            var cancellationParameter = Expression.Parameter(typeof(CancellationToken), "cancellation");

            var convertedMessageDispatcher = Expression.Convert(messageDispatcherParameter, remoteMessageDispatcherType);
            var dispatchLocalAsyncCall = Expression.Call(
                convertedMessageDispatcher,
                dispatchLocalAsyncMethod,
                dispatchDataParameter,
                publishParameter,
                cancellationParameter);

            _dispatchLocalAsync = Expression.Lambda<Func<IMessageDispatcher, DispatchDataDictionary, bool, CancellationToken, ValueTask<IDispatchResult>>>(
               dispatchLocalAsyncCall,
               messageDispatcherParameter,
               dispatchDataParameter,
               publishParameter,
               cancellationParameter).Compile();
        }

        public static ValueTask<IDispatchResult> DispatchLocalAsync(
            this IMessageDispatcher messageDispatcher,
            DispatchDataDictionary dispatchData,
            bool publish,
            CancellationToken cancellation = default)
        {
            if (messageDispatcher == null)
                throw new ArgumentNullException(nameof(messageDispatcher));

            if (_isRemoteMessageDispatcher(messageDispatcher))
            {
                return _dispatchLocalAsync(messageDispatcher, dispatchData, publish, cancellation);
            }

            return messageDispatcher.DispatchAsync(dispatchData, publish, cancellation);
        }
    }
}
