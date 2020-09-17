using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using MongoDB.Bson;
using MongoDB.Driver;
using Proto.Cluster.IdentityLookup;

namespace Proto.Cluster.MongoIdentityLookup
{
    public class MongoIdentityLookup : IIdentityLookup
    {
        private const string MongoPlacementActorName = "placement-activator";
        private readonly string _clusterName;
        private readonly IMongoCollection<PidLookupEntity> _pids;
        private Cluster _cluster;
        private IMongoDatabase _db;
        private bool _isClient;
        private ILogger _logger;
        private MemberList _memberList;
        private PID _placementActor;
        private ActorSystem _system;

        public MongoIdentityLookup(string clusterName, IMongoDatabase db)
        {
            _clusterName = clusterName;
            _db = db;
            _pids = db.GetCollection<PidLookupEntity>("pids");
        }

        public async Task<PID> GetAsync(string identity, string kind, CancellationToken ct)
        {
            var key = $"{_clusterName}-{kind}-{identity}";
            var existingPid = await TryGetExistingActivationAsync(key, identity, kind, ct);
            //we got an existing activation, use this
            if (existingPid != null)
            {
                return existingPid;
            }

            //are there any members that can spawn this kind?
            //if not, just bail out
            var activator = _memberList.GetActivator(kind);
            if (activator == null)
            {
                return null;
            }
            
            //try to acquire global lock for this key
            var locked = await TryAcquireLockAsync(key, identity, kind);
            if (locked)
            {
                //we have the lock, spawn and return
                return await SpawnActivationAsync(key, identity, kind, activator, ct);
            }

            //we didn't get the lock, spin read for x times before giving up
            return await SpinReadAsync(key, identity, kind, ct);
        }

        private async Task<bool> TryAcquireLockAsync(string key, string identity, string kind)
        {
            var requestId = Guid.NewGuid();
            var lockEntity = new PidLookupEntity
            {
                Address = null,
                Identity = identity,
                Key = key,
                Kind = kind,
                LockedBy = requestId.ToString()
            };
            var l = await _pids.ReplaceOneAsync(x => x.Key == key && x.LockedBy == null, lockEntity, new ReplaceOptions
                {
                    IsUpsert = true
                }
            );

            //inserted
            if (l.UpsertedId.IsString)
                return true;
            
            return l.ModifiedCount == 1;
        }

        private async Task<PID> SpawnActivationAsync(string key, string identity, string kind, Member activator,
            CancellationToken ct)
        {
            //we own the lock
            _logger.LogInformation("Storing placement lookup for {Identity} {Kind}", identity, kind);

            var remotePid = RemotePlacementActor(activator.Address);
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
                    Address = activator.Address,
                    Identity = identity,
                    UniqueIdentity = resp.Pid.Id,
                    Key = key,
                    Kind = kind,
                    MemberId = activator.Id,
                    LockedBy = null
                };

                await _pids.ReplaceOneAsync(
                    s => s.Key == key,
                    entry, new ReplaceOptions
                    {
                        IsUpsert = true
                    }, CancellationToken.None
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

        private async Task<PID> SpinReadAsync(string key, string identity, string kind, CancellationToken ct)
        {
            for (int i = 0; i < 10; i++)
            {
                var e = await TryGetExistingActivationAsync(key, identity, kind, ct);
                if (e != null)
                {
                    return e;
                }

                //just wait and try again
                await Task.Delay(100);
            }

            //failed to get pid, bail out
            return null;
        }

        private async Task<PID> TryGetExistingActivationAsync(string key, string identity, string kind,
            CancellationToken ct)
        {
            var pidLookup = await _pids.Find(x => x.Key == key).Limit(1).SingleOrDefaultAsync(ct);
            if (pidLookup == null) return null;
            var memberExists = _memberList.ContainsMemberId(pidLookup.MemberId);
            if (!memberExists)
            {
                _logger.LogWarning(
                    "Found placement lookup for {Identity} {Kind}, but Member {MemberId} is not part of cluster", identity,
                    kind, pidLookup.MemberId
                );
                //remove this one, it's outdated
                await RemoveUniqueIdentityAsync(pidLookup.UniqueIdentity);
                return null;
            }
            
            var isLocked = pidLookup.LockedBy != null;
            if (isLocked)
            {
                return null;
            }

            var pid = new PID(pidLookup.Address, pidLookup.UniqueIdentity);
            return pid;
        }

        public Task SetupAsync(Cluster cluster, string[] kinds, bool isClient)
        {
            _cluster = cluster;
            _system = cluster.System;
            _memberList = cluster.MemberList;
            _logger = Log.CreateLogger("MongoIdentityLookup-" + cluster.LoggerId);
            _isClient = isClient;

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
            var props = Props.FromProducer(() => new MongoPlacementActor(_cluster,this));
            _placementActor = _system.Root.SpawnNamed(props, MongoPlacementActorName);

            return Task.CompletedTask;
        }

        public async Task ShutdownAsync()
        {
            if (!_isClient) await _cluster.System.Root.PoisonAsync(_placementActor);

            await RemoveMemberAsync(_cluster.Id.ToString());
        }

        private Task RemoveMemberAsync(string memberId)
        {
            return _pids.DeleteManyAsync(p => p.MemberId == memberId);
        }

        private PID RemotePlacementActor(string address)
        {
            return new PID(address, MongoPlacementActorName);
        }

        public Task RemoveUniqueIdentityAsync(string uniqueIdentity)
        {
            return _pids.DeleteManyAsync(p => p.UniqueIdentity == uniqueIdentity);
        }
    }
}