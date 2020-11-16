namespace Proto.Remote.GrpcCore
{
    public static class RemoteExtensions
    {
        public static ActorSystem WithRemote(this ActorSystem system, GrpcCoreRemoteConfig remoteConfig)
        {
            var _ = new GrpcCoreRemote(system, remoteConfig);
            return system;
        }
    }
}