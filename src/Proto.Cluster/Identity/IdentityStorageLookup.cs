namespace Proto.Cluster.Identity
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using IdentityLookup;
    using Router;

    public class IdentityStorageLookup : IIdentityLookup
    {
        internal IIdentityStorage Storage { get; }
        private const string PlacementActorName = "placement-activator";
        private static readonly int PidClusterIdentityStartIndex = PlacementActorName.Length + 1;
        internal Cluster Cluster;
        private bool _isClient;
        internal MemberList MemberList;
        private PID _placementActor;
        private ActorSystem _system;
        private PID _router;
        private string _memberId;

        public IdentityStorageLookup(IIdentityStorage storage)
        {
            Storage = storage;
        }

        public async Task<PID?> GetAsync(ClusterIdentity clusterIdentity, CancellationToken ct)
        {
            var msg = new GetPid(clusterIdentity, ct);

            var res = await _system.Root.RequestAsync<PidResult>(_router, msg, ct);
            return res?.Pid;
        }

        public Task SetupAsync(Cluster cluster, string[] kinds, bool isClient)
        {
            Cluster = cluster;
            _system = cluster.System;
            _memberId = cluster.Id.ToString();
            MemberList = cluster.MemberList;
            _isClient = isClient;

            var workerProps = Props.FromProducer(() => new IdentityStorageWorker(this));
            //TODO: should pool size be configurable?

            var routerProps = _system.Root.NewConsistentHashPool(workerProps, 50);

            _router = _system.Root.Spawn(routerProps);

            //hook up events
            cluster.System.EventStream.Subscribe<ClusterTopology>(e =>
                {
                    //delete all members that have left from the lookup
                    foreach (var left in e.Left)
                        //YOLO. event stream is not async
                        _ = RemoveMemberAsync(left.Id);
                }
            );

            if (isClient) return Task.CompletedTask;
            var props = Props.FromProducer(() => new IdentityStoragePlacementActor(Cluster, this));
            _placementActor = _system.Root.SpawnNamed(props, PlacementActorName);

            return Task.CompletedTask;
        }

        public async Task ShutdownAsync()
        {
            if (!_isClient) await Cluster.System.Root.PoisonAsync(_placementActor);

            await RemoveMemberAsync(_memberId);
        }

        internal Task RemoveMemberAsync(string memberId)
        {
            return Storage.RemoveMemberIdAsync(memberId, CancellationToken.None);
        }

        internal PID RemotePlacementActor(string address)
        {
            return PID.FromAddress(address, PlacementActorName);
        }

        public Task RemovePidAsync(PID pid, CancellationToken ct)
        {
            return Storage.RemoveActivation(pid, ct);
        }

        public static bool TryGetClusterIdentityShortString(string pidId, out string? clusterIdentity)
        {
            var idIndex = pidId.LastIndexOf("$", StringComparison.Ordinal);
            if (idIndex > PidClusterIdentityStartIndex)
            {
                clusterIdentity = pidId.Substring(PidClusterIdentityStartIndex,
                    idIndex - PidClusterIdentityStartIndex
                );
                return true;
            }

            clusterIdentity = default;
            return false;
        }
    }
}