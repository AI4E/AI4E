using System;

namespace AI4E.SignalR.Server
{
    public sealed class SecurityTokenGenerator : ISecurityTokenGenerator
    {
        public string GenerateSecurityToken(string clientId, DateTime now)
        {
            return Guid.NewGuid().ToString();
        }
    }
}
