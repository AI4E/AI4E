using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace BookStore.Alerts
{
    public readonly struct AlertPlacement : IEquatable<AlertPlacement>, IDisposable
    {
        private readonly IAlertMessages _alertMessages;

        internal AlertPlacement(
            IAlertMessages alertMessages,
            LinkedListNode<AlertMessage> node)
        {
            Debug.Assert(alertMessages != null);

            _alertMessages = alertMessages;
            Node = node;
        }

        internal LinkedListNode<AlertMessage> Node { get; }

        public void Dispose()
        {
            _alertMessages?.CancelAlert(this);
        }

        public bool Equals(AlertPlacement other)
        {
            // TODO: Is it necessary to include the AlertMessageManager into comparison?
            // A node belongs to a single AlertMessageManager in its complete lifetime,
            // so it should be suffice to compare the nodes.

            return Node == other.Node;
        }

        public override bool Equals(object obj)
        {
            return obj is AlertPlacement alertPlacement && Equals(alertPlacement);
        }

        public override int GetHashCode()
        {
            // TODO: Is it necessary to include the AlertMessageManager into hash code generation?
            //       See Equals(AlertPlacement)

            return Node.GetHashCode();
        }

        public static bool operator ==(AlertPlacement left, AlertPlacement right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(AlertPlacement left, AlertPlacement right)
        {
            return !left.Equals(right);
        }
    }
}
