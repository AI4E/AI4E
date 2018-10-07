using System;
using static System.Diagnostics.Debug;

namespace AI4E.Handler
{
    public sealed class MessageHandlerContextDescriptor
    {
        private readonly Action<object, IMessageDispatchContext> _contextSetter;
        private readonly Action<object, IMessageDispatcher> _dispatcherSetter;

        internal MessageHandlerContextDescriptor(Action<object, IMessageDispatchContext> contextSetter,
                                                 Action<object, IMessageDispatcher> dispatcherSetter)
        {
            Assert(contextSetter != null);
            Assert(dispatcherSetter != null);

            _contextSetter = contextSetter;
            _dispatcherSetter = dispatcherSetter;
        }

        public bool CanSetContext => _contextSetter != null;
        public bool CanSetDispatcher => _dispatcherSetter != null;

        public void SetContext(object handler, IMessageDispatchContext dispatchContext)
        {
            if (handler == null)
                throw new ArgumentNullException(nameof(handler));

            if (_contextSetter == null)
                throw new InvalidOperationException();

            _contextSetter(handler, dispatchContext);
        }

        public void SetDispatcher(object handler, IMessageDispatcher messageDispatcher)
        {
            if (handler == null)
                throw new ArgumentNullException(nameof(handler));

            if (_contextSetter == null)
                throw new InvalidOperationException();

            _dispatcherSetter(handler, messageDispatcher);
        }
    }
}
