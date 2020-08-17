namespace Proto.Cluster
{
    internal class PidCacheUpdater
    {
        private readonly PidCache _cache;
        private PID _watcher = new PID();

        internal PidCacheUpdater(Cluster cluster, PidCache cache)
        {
            Cluster = cluster;
            _cache = cache;
        }

        private Cluster Cluster { get; }

        internal void Setup()
        {
            var props = Props.FromProducer(() => new PidCacheWatcher(_cache))
                .WithGuardianSupervisorStrategy(Supervision.AlwaysRestartStrategy);
            _watcher = Cluster.System.Root.SpawnNamed(props, "PidCacheWatcher");
        }

        internal void Shutdown()
        {
            Cluster.System.Root.Stop(_watcher);
        }

        internal void Watch(PID pid)
        {
            Cluster.System.Root.Send(_watcher, new WatchPidRequest(pid));
        }
    }
}