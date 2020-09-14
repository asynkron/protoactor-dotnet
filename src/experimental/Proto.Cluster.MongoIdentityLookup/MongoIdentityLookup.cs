using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using MongoDB.Bson;
using MongoDB.Driver;
using Proto.Cluster.IdentityLookup;
using Proto.Cluster.Partition;

namespace Proto.Cluster.MongoIdentityLookup
{
    public class MongoIdentityLookup : IIdentityLookup
    {
        private const string MongoPlacementActorName = "placement-activator";
        private readonly string _clusterName;
        private Cluster _cluster;
        private IMongoDatabase _db;
        private string[] _kinds;
        private readonly IMongoCollection<PidLookupEntity> _pids;
        private MemberList _memberList;
        private ActorSystem _system;
        private PID _placementActor;
        private ILogger _logger;

        public MongoIdentityLookup(string clusterName, IMongoDatabase db)
        {
            _clusterName = clusterName;
            _db = db;
            _pids = db.GetCollection<PidLookupEntity>("pids");
        }

        public async Task<PID> GetAsync(string identity, string kind, CancellationToken ct)
        {
            var key = $"{_clusterName}-{kind}-{identity}";
            var pidLookup = _pids.AsQueryable().FirstOrDefault(x => x.Key == key);
            if (pidLookup != null)
            {
                var pid = new PID(pidLookup.Address, pidLookup.UniqueIdentity);
                return pid;
            }

            var activator = _memberList.GetActivator(kind);
            var remotePid = RemotePlacementActor(activator);
            var req = new ActivationRequest
            {
                Kind = kind,
                Identity = identity
            };

            try
            {
                var resp = ct == CancellationToken.None
                    ? await _cluster.System.Root.RequestAsync<ActivationResponse>(remotePid, req,
                        _cluster.Config!.TimeoutTimespan
                    )
                    : await _cluster.System.Root.RequestAsync<ActivationResponse>(remotePid, req, ct);

                var entry = new PidLookupEntity
                {
                    Address = activator,
                    Id = ObjectId.Empty,
                    Identity = identity,
                    UniqueIdentity = resp.Pid.Id,
                    Key = key,
                    Kind = kind,
                    MemberId = _cluster.Id.ToString()
                };

                await _pids.ReplaceOneAsync(
                    s => s.Key == key,
                    entry, new ReplaceOptions
                    {
                        IsUpsert = true
                    }, cancellationToken: CancellationToken.None
                );
                
                return resp.Pid;
            }
            //TODO: decide if we throw or return null
            catch (TimeoutException)
            {
                _logger.LogDebug("Remote PID request timeout {@Request}", req);
                return null;
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Error occured requesting remote PID {@Request}", req);
                return null;
            }
        }

        private Task RemoveMemberAsync(string memberId) => _pids.DeleteManyAsync(p => p.MemberId == memberId);

        public Task SetupAsync(Cluster cluster, string[] kinds, bool isClient)
        {
            _cluster = cluster;
            _system = cluster.System;
            _kinds = kinds;
            _memberList = cluster.MemberList;
            _logger = Log.CreateLogger("MongoIdentityLookup-" + cluster.LoggerId);

            //hook up events
            cluster.System.EventStream.Subscribe<ClusterTopology>(e =>
                {
                    //delete all members that have left from the lookup
                    foreach (var left in e.Left)
                    {
                        //YOLO. event stream is not async
                        _ = RemoveMemberAsync(left.Id);
                    }
                }
            );

            var props = Props.FromProducer(() => new MongoPlacementActor(_cluster));
            _placementActor = _system.Root.SpawnNamed(props, MongoPlacementActorName);

            return Task.CompletedTask;
        }

        public async Task ShutdownAsync()
        {
            await _cluster.System.Root.PoisonAsync(_placementActor);
            await RemoveMemberAsync(_cluster.Id.ToString());
        }

        private PID RemotePlacementActor(string address) => new PID(address, MongoPlacementActorName);
    }
}