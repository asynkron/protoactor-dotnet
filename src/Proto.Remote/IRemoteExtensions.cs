using System;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading.Tasks;

namespace Proto.Remote
{
    public static class IRemoteExtensions
    {
        public static IRemote Remote(this ActorSystem system) => system.Extensions.Get<IRemote>()!;

        public static IRemote Remote(this IContext context) => context.System.Remote();

        /// <summary>
        ///     Spawn a remote actor with auto-generated name
        /// </summary>
        /// <param name="address">Remote node address</param>
        /// <param name="kind">Actor kind, must be known on the remote node</param>
        /// <param name="timeout">Timeout for the confirmation to be received from the remote node</param>
        /// <returns></returns>
        public static Task<ActorPidResponse> SpawnAsync(this IRemote remote, string address, string kind, TimeSpan timeout) =>
            SpawnNamedAsync(remote, address, "", kind, timeout);

        /// <summary>
        ///     Spawn a remote actor with a name
        /// </summary>
        /// <param name="address">Remote node address</param>
        /// <param name="name">Remote actor name</param>
        /// <param name="kind">Actor kind, must be known on the remote node</param>
        /// <param name="timeout">Timeout for the confirmation to be received from the remote node</param>
        /// <returns></returns>
        public static async Task<ActorPidResponse> SpawnNamedAsync(this IRemote remote, string address, string name, string kind, TimeSpan timeout)
        {
            var activator = ActivatorForAddress(address);

            var res = await remote.System.Root.RequestAsync<ActorPidResponse>(
                activator, new ActorPidRequest
                {
                    Kind = kind,
                    Name = name
                }, timeout
            );

            return res;

            static PID ActivatorForAddress(string address) => PID.FromAddress(address, "activator");
        }


        public static bool IsClientAddress(this PID pid) => pid.Address.StartsWith(ActorSystem.Client);
        public static bool IsClientID(this PID pid) => pid.Id.StartsWith(ActorSystem.Client);
        public static bool TryTranslateToLocalClientPID(this PID pid, [NotNullWhen(true)] out PID? clientPid)
        {
            if (pid.IsClientID())
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
            else if (pid.Address == system.Address && pid.IsClientID())
            {
                systemId = pid.Id.Split("/")[1];
                return true;
            }
            systemId = null;
            return false;
        }
    }
}