using System;

namespace AI4E.SignalR.Server
{
    public interface ISecurityTokenGenerator
    {
        string GenerateSecurityToken(string clientId, DateTime now);
    }
}
