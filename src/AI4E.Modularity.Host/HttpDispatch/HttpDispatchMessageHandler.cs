v

using System;

namespace AI4E.Modularity.HttpDispatch
{
    public sealed class HttpDispatchMessageHandler : MessageHandler
    {
        private readonly HttpDispatchTable _table;

        public HttpDispatchMessageHandler(HttpDispatchTable table)
        {
            if (table == null)
                throw new ArgumentNullException(nameof(table));

            _table = table;
        }

        public IDispatchResult Handle(RegisterHttpPrefix registerPrefix)
        {
            if (_table.Register(registerPrefix.Prefix, registerPrefix.EndPoint))
            {
                return Success();
            }

            return Failure();
        }

        public IDispatchResult Handle(UnregisterHttpPrefix unregisterPrefix)
        {
            if (_table.Unregister(unregisterPrefix.Prefix))
            {
                return Success();
            }

            return Failure();
        }

        public void Handle(EndPointDisconnected endPointDisconnected)
        {
            _table.Unregister(endPointDisconnected.EndPoint);
        }
    }
}
