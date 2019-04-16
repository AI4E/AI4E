using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace BookStore.Alerts
{
    public readonly struct Alert : IEquatable<Alert>
    {
        private readonly AlertMessageManager _alertMessageManager;

        internal Alert(
            AlertMessageManager alertMessageManager,
            LinkedListNode<AlertMessage> node)
        {
            Debug.Assert(alertMessageManager != null);
            Debug.Assert(node != null);
            _alertMessageManager = alertMessageManager;
            Node = node;
        }

        internal LinkedListNode<AlertMessage> Node { get; }

        public bool Equals(Alert other)
        {
            // TODO: Is it necessary to include the AlertMessageManager into comparison?
            // A node belongs to a single AlertMessageManager in its complete lifetime,
            // so it should be suffice to compare the nodes.

            return Node == other.Node;
        }

        public override bool Equals(object obj)
        {
            return obj is Alert alert && Equals(alert);
        }

        public override int GetHashCode()
        {
            // TODO: Is it necessary to include the AlertMessageManager into hash code generation?
            //       See Equals(Alert)

            return Node.GetHashCode();
        }

        public static bool operator ==(Alert left, Alert right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(Alert left, Alert right)
        {
            return !left.Equals(right);
        }

        public bool IsExpired => Node == null || Node.List == null;

        /// <summary>
        /// Gets the type of alert.
        /// </summary>
        public AlertType AlertType => Node?.Value.AlertType ?? AlertType.None;

        /// <summary>
        /// Gets the alert message.
        /// </summary>
        public string Message => Node?.Value.Message;

        /// <summary>
        /// Gets a boolean value indicating whether the alert may be dismissed.
        /// </summary>
        public bool AllowDismiss => !IsExpired && Node.Value.AllowDismiss;

        // TODO: Do we need another property for the expiration?

        public void Dismiss()
        {
            _alertMessageManager?.Dismiss(this);
        }
    }
}
