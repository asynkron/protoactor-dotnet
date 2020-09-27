using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using MongoDB.Driver;

namespace Proto.Cluster.MongoIdentityLookup
{
    public class MongoIdentityWorker : IActor
    {
        private readonly Cluster _cluster;
        private readonly ILogger _logger = Log.CreateLogger<MongoIdentityWorker>();
        private readonly MongoIdentityLookup _lookup;
        private readonly MemberList _memberList;
        private readonly IMongoCollection<PidLookupEntity> _pids;
 

        public MongoIdentityWorker(MongoIdentityLookup lookup)
        {
            _cluster = lookup.Cluster;
            _pids = lookup.Pids;
            _memberList = lookup.MemberList;
            _lookup = lookup;
        }

        public async Task ReceiveAsync(IContext context)
        {
            try
            {
                if (context.Message is GetPid msg)
                {
                    if (_cluster.PidCache.TryGet(msg.Kind, msg.Identity, out var existing))
                    {
                        context.Respond(new PidResult
                            {
                                Pid = existing
                            }
                        );
                        return;
                    }

                    var pid = await GetWithGlobalLock(msg.Key, msg.Identity, msg.Kind, CancellationToken.None);
                    context.Respond(new PidResult
                        {
                            Pid = pid
                        }
                    );
                }
            }
            catch (Exception x)
            {
                _logger.LogError(x, "Mongo Identity worker crashed {Id}", context.Self!.ToShortString());
                throw;
            }
        }

        private async Task<PID> GetWithGlobalLock(string key, string identity, string kind, CancellationToken ct)
        {
            var existingPid = await TryGetExistingActivationAsync(key, identity, kind, ct);
            //we got an existing activation, use this
            if (existingPid != null) return existingPid;

            //are there any members that can spawn this kind?
            //if not, just bail out
            var activator = _memberList.GetActivator(kind);
            if (activator == null) return null;

            //try to acquire global lock for this key
            var requestId = Guid.NewGuid().ToString();
            var weOwnTheLock = await TryAcquireLockAsync(key, identity, kind, requestId);

            
            //we didn't get the lock, spin read for x times before giving up
            if (!weOwnTheLock) return await SpinWaitOnLockAsync(key, identity, kind, ct);
            
            //we have the lock, spawn and return
            var pid = await SpawnActivationAsync(key, identity, kind, activator, requestId, ct);
            //update cache
            _cluster.PidCache.TryAdd(kind, identity, pid);
            return pid;
        }

        private async Task<bool> TryAcquireLockAsync(string key, string identity, string kind, string requestId)
        {
            var lockEntity = new PidLookupEntity
            {
                Address = null,
                Identity = identity,
                Key = key,
                Kind = kind,
                LockedBy = requestId,
                Revision = 1,
                MemberId = null
            };
            try
            {
                //we 100% sure own the lock here
                await _pids.InsertOneAsync(lockEntity, new InsertOneOptions());
                return true;
            }
            catch (MongoWriteException)
            {
                
                var l = await _pids.ReplaceOneAsync(x => x.Key == key && x.LockedBy == null && x.Revision == 0,
                    lockEntity,
                    new ReplaceOptions
                    {
                        IsUpsert = false
                    }
                );
                
                //if l.MatchCount == 1, then one document was updated by us, and we should own the lock, no?
                return l.IsAcknowledged && l.ModifiedCount == 1;
            }
        }

        private async Task<PID> SpawnActivationAsync(string key, string identity, string kind, Member activator,
            string requestId, CancellationToken ct)
        {
            //we own the lock
            _logger.LogDebug("Storing placement lookup for {Identity} {Kind}", identity, kind);

            var remotePid = _lookup.RemotePlacementActor(activator.Address);
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

                var res = await _pids.UpdateOneAsync(
                    s => s.Key == key && s.LockedBy == requestId && s.Revision == 1,
                    Builders<PidLookupEntity>.Update
                        .Set(l => l.Address, activator.Address)
                        .Set(l => l.MemberId, activator.Id)
                        .Set(l => l.UniqueIdentity, resp.Pid.Id)
                        .Set(l => l.Revision, 2)
                        .Unset(l => l.LockedBy)
                    , new UpdateOptions(), CancellationToken.None
                );

                //nothing was updated
                if (res.MatchedCount != 1)
                {
                    //meaning, we spawned an actor but its placement is not stored anywhere
                    _logger.LogCritical("No entry was updated {Key}",key);
                }

                return resp.Pid;
            }
            //TODO: decide if we throw or return null
            catch (TimeoutException)
            {
                _logger.LogDebug("Remote PID request timeout {@Request}", req);
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Error occured requesting remote PID {@Request}", req);
            }

            //Clean up our mess..
            await DeleteLock(key, requestId, ct);
            return null;
        }


        private async Task<PID> SpinWaitOnLockAsync(string key, string identity, string kind, CancellationToken ct)
        {
            var pidLookupEntity = await LookupKey(key, ct);
            var lockId = pidLookupEntity?.LockedBy;
            if (lockId != null)
            {
                //There is an active lock on the pid, spin wait
                var i = 0;
                do
                {
                    await Task.Delay(20 * i, ct);
                } while ((pidLookupEntity = await LookupKey(key, ct))?.LockedBy == lockId && ++i < 10);
            }

            //the lookup entity was lost, stale lock maybe?
            if (pidLookupEntity == null) return null;
            
            //lookup was unlocked, return this pid
            if (pidLookupEntity.LockedBy == null) return await ValidateAndMapToPid(identity, kind, pidLookupEntity);
            
            //Still locked but not by the same request that originally locked it, so not stale
            if (pidLookupEntity.LockedBy != lockId) return null;

            //Stale lock. just delete it and let cluster retry
            _logger.LogDebug($"Stale lock: {pidLookupEntity.Key}");
            await DeleteLock(key, lockId, CancellationToken.None);
            return null;
        }

        private async Task<PID> TryGetExistingActivationAsync(string key, string identity, string kind,
            CancellationToken ct)
        {
            var pidLookup = await LookupKey(key, ct);
            if (pidLookup == null) return null;
            return await ValidateAndMapToPid(identity, kind, pidLookup);
        }

        private async Task<PID> ValidateAndMapToPid(string identity, string kind, PidLookupEntity pidLookup)
        {
            var isLocked = pidLookup.LockedBy != null;
            if (isLocked) return null;
            
            var memberExists = pidLookup.MemberId == null || _memberList.ContainsMemberId(pidLookup.MemberId);
            if (!memberExists)
            {
                _logger.LogWarning(
                    "Found placement lookup for {Identity} {Kind}, but Member {MemberId} is not part of cluster",
                    identity,
                    kind, pidLookup.MemberId
                );
                //remove this one, it's outdated
                await _lookup.RemoveUniqueIdentityAsync(pidLookup.UniqueIdentity);
                return null;
            }

            var pid = new PID(pidLookup.Address, pidLookup.UniqueIdentity);
            return pid;
        }

        private async Task<PidLookupEntity> LookupKey(string key, CancellationToken ct)
        {
            return await _pids.Find(x => x.Key == key).Limit(1).SingleOrDefaultAsync(ct);
        }

        private async Task DeleteLock(string key, string requestId, CancellationToken ct)
        {
            var res = await _pids.DeleteOneAsync(x => x.Key == key && x.LockedBy == requestId, ct);
            if (res.DeletedCount == 0) _logger.LogError("Deleted lock {Key} failed", key);
        }
    }
}