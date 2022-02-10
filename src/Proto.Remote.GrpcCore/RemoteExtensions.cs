using System;
using JetBrains.Annotations;

namespace Proto.Remote.GrpcCore
{
    [PublicAPI]
    public static class RemoteExtensions
    {
        [Obsolete(ObsoleteInformation.Text)]
        public static ActorSystem WithRemote(this ActorSystem system, GrpcCoreRemoteConfig remoteConfig)
        {
            _ = new GrpcCoreRemote(system, remoteConfig);
            return system;
        }
    }
}