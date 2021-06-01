using System;
using System.Threading;
using System.Threading.Tasks;

namespace Proto.Cluster.Identity
{
    public class IdentityStorageLookup : IIdentityLookup
    {
        private const string PlacementActorName = "placement-activator";
        private static readonly int PidClusterIdentityStartIndex = PlacementActorName.Length + 1;
        private bool _isClient;
        private string _memberId = string.Empty;
        private PID _placementActor = null!;
        private ActorSystem _system = null!;
        private PID _worker = null!;
        internal Cluster Cluster = null!;
        internal MemberList MemberList = null!;

        public IdentityStorageLookup(IIdentityStorage storage) => Storage = storage;

        internal IIdentityStorage Storage { get; }

        public async Task<PID?> GetAsync(ClusterIdentity clusterIdentity, CancellationToken ct)
        {
            var msg = new GetPid(clusterIdentity, ct);

            var res = await _system.Root.RequestAsync<PidResult>(_worker, msg, ct);
            return res?.Pid;
        }

        public async Task SetupAsync(Cluster cluster, string[] kinds, bool isClient)
        {
            Cluster = cluster;
            _system = cluster.System;
            _memberId = cluster.System.Id;
            MemberList = cluster.MemberList;
            _isClient = isClient;
            await Storage.Init();

            cluster.System.Metrics.Register(new IdentityMetrics(cluster.System.Metrics));

            var workerProps = Props.FromProducer(() => new IdentityStorageWorker(this));
            _worker = _system.Root.Spawn(workerProps);

            //hook up events
            cluster.System.EventStream.Subscribe<ClusterTopology>(e => {
                    //delete all members that have left from the lookup
                    foreach (var left in e.Left)
                        //YOLO. event stream is not async
                    {
                        _ = RemoveMemberAsync(left.Id);
                    }
                }
            );

            if (isClient) return;

            var props = Props.FromProducer(() => new IdentityStoragePlacementActor(Cluster, this));
            _placementActor = _system.Root.SpawnNamed(props, PlacementActorName);
        }

        public async Task ShutdownAsync()
        {
            await Cluster.System.Root.StopAsync(_worker);
            if (!_isClient) await Cluster.System.Root.StopAsync(_placementActor);

            await RemoveMemberAsync(_memberId);
        }

        public Task RemovePidAsync(ClusterIdentity clusterIdentity, PID pid, CancellationToken ct)
        {
            if (_system.Shutdown.IsCancellationRequested) return Task.CompletedTask;

            return Storage.RemoveActivation(clusterIdentity, pid, ct);
        }

        internal Task RemoveMemberAsync(string memberId) => Storage.RemoveMember(memberId, CancellationToken.None);

        internal PID RemotePlacementActor(string address) => PID.FromAddress(address, PlacementActorName);
    }
}