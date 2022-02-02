using System;
using System.Diagnostics.CodeAnalysis;
using System.Linq;

namespace Proto.Remote
{
    public static class PidExtensions
    {
        public static bool IsClientAddress(this PID pid) => pid.Address.StartsWith(ActorSystem.Client, StringComparison.Ordinal);
        public static bool IsClientId(this PID pid) => pid.Id.StartsWith(ActorSystem.Client, StringComparison.Ordinal);
        public static bool TryTranslateToLocalClientPID(this PID pid, [NotNullWhen(true)] out PID? clientPid)
        {
            if (pid.IsClientId())
            {
                var parts = pid.Id.Split("/");
                var address = $"{parts[0]}/{parts[1]}";
                var id = string.Join("/", parts.Skip(2));
                clientPid = PID.FromAddress(address, id);
                clientPid.RequestId = pid.RequestId;
                return true;
            }
            clientPid = null;
            return false;
        }
        public static bool TryTranslateToProxyPID(this PID pid, ActorSystem system, string proxyAddress, [NotNullWhen(true)] out PID? clientPid)
        {
            if (pid.IsClientAddress())
            {
                clientPid = PID.FromAddress(proxyAddress, $"{ActorSystem.Client}/{system.Id}/{pid.Id}");
                clientPid.RequestId = pid.RequestId;
                return true;
            }
            clientPid = null;
            return false;
        }
        public static bool TryGetSystemId(this PID pid, ActorSystem system, [NotNullWhen(true)] out string? systemId)
        {
            if (pid.IsClientAddress())
            {
                systemId = pid.Address.Split("/")[1];
                return true;
            }
            else if (pid.Address == system.Address && pid.IsClientId())
            {
                systemId = pid.Id.Split("/")[1];
                return true;
            }
            systemId = null;
            return false;
        }
    }
}