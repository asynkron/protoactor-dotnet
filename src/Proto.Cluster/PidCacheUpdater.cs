namespace Proto.Cluster
{
    class PidCacheUpdater
    {
        private PID _watcher = new PID();
        private Subscription<object>? _clusterTopologyEvnSub;
        private readonly PidCache _cache;
        private Cluster Cluster { get; }
        internal PidCacheUpdater(Cluster cluster, PidCache cache)
        {
            Cluster = cluster;
            _cache = cache;
        }

        internal void Setup()
        {
            var props = Props.FromProducer(() => new PidCacheWatcher(_cache)).WithGuardianSupervisorStrategy(Supervision.AlwaysRestartStrategy);
            _watcher = Cluster.System.Root.SpawnNamed(props, "PidCacheWatcher");
            _clusterTopologyEvnSub = Cluster.System.EventStream.Subscribe<MemberStatusEvent>(_cache.OnMemberStatusEvent);
        }
        
        internal void Stop()
        {
            Cluster.System.Root.Stop(_watcher);
            Cluster.System.EventStream.Unsubscribe(_clusterTopologyEvnSub);
        }

        internal void Watch(PID pid)
        {
            Cluster.System.Root.Send(_watcher, new WatchPidRequest(pid));
        }
        
    }
}