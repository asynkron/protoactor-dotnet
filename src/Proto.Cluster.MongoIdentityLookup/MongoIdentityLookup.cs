using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using MongoDB.Driver;
using Proto.Cluster.IdentityLookup;
using Proto.Router;

namespace Proto.Cluster.MongoIdentityLookup
{
    public class MongoIdentityLookup : IIdentityLookup
    {
        private const string MongoPlacementActorName = "placement-activator";
        private readonly string _clusterName;
        internal readonly IMongoCollection<PidLookupEntity> Pids;
        internal Cluster Cluster;
        private IMongoDatabase _db;
        private bool _isClient;
        private ILogger _logger;
        internal MemberList MemberList;
        private PID _placementActor;
        private ActorSystem _system;
        private PID _router;

        public MongoIdentityLookup(string clusterName, IMongoDatabase db)
        {
            _clusterName = clusterName;
            _db = db;
            //TODO: make collection name configurable
            Pids = db.GetCollection<PidLookupEntity>("pids");
        }

        public Task<PID> GetAsync(string identity, string kind, CancellationToken ct)
            => GetAsync(new ClusterIdentity {Identity = identity, Kind = kind}, ct);

        public async Task<PID> GetAsync(ClusterIdentity clusterIdentity, CancellationToken ct)
        {
            var key = $"{_clusterName}-{clusterIdentity.Kind}-{clusterIdentity.Identity}";

            var msg = new GetPid
            {
                Key = key,
                ClusterIdentity = clusterIdentity,
                CancellationToken = ct
            };

            var res = await _system.Root.RequestAsync<PidResult>(_router, msg, ct);
            return res.Pid;
        }

        public Task SetupAsync(Cluster cluster, string[] kinds, bool isClient)
        {
            Cluster = cluster;
            _system = cluster.System;
            MemberList = cluster.MemberList;
            _logger = Log.CreateLogger("MongoIdentityLookup-" + cluster.LoggerId);
            _isClient = isClient;

            var workerProps = Props.FromProducer(() => new MongoIdentityWorker(this));
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
            var props = Props.FromProducer(() => new MongoPlacementActor(Cluster, this));
            _placementActor = _system.Root.SpawnNamed(props, MongoPlacementActorName);

            return Task.CompletedTask;
        }

        public async Task ShutdownAsync()
        {
            if (!_isClient) await Cluster.System.Root.PoisonAsync(_placementActor);

            await RemoveMemberAsync(Cluster.Id.ToString());
        }

        internal Task RemoveMemberAsync(string memberId)
        {
            return Pids.DeleteManyAsync(p => p.MemberId == memberId);
        }

        internal PID RemotePlacementActor(string address)
        {
            return PID.FromAddress(address, MongoPlacementActorName);
        }

        public Task RemoveUniqueIdentityAsync(string uniqueIdentity)
        {
            return Pids.DeleteManyAsync(p => p.UniqueIdentity == uniqueIdentity);
        }
    }
}