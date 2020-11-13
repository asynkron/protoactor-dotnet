namespace Proto.Remote
{
    public static class RemoteExtensions
    {
        public static ActorSystem WithRemote(this ActorSystem system, RemoteConfig remoteConfig)
        {
            var _ = new Remote(system, remoteConfig);
            return system;
        }
    }
}