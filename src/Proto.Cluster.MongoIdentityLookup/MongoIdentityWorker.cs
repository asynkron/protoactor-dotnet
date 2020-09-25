using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using MongoDB.Driver;

namespace Proto.Cluster.MongoIdentityLookup
{
    public class MongoIdentityWorker : IActor
    {
        private readonly MongoIdentityLookup _lookup;
        private readonly ILogger _logger = Log.CreateLogger<MongoIdentityWorker>();
        private readonly MemberList _memberList;
        private readonly IMongoCollection<PidLookupEntity> _pids;
        private readonly Cluster _cluster;


        public MongoIdentityWorker(MongoIdentityLookup lookup)
        {
            _cluster = lookup.Cluster;
            _pids = lookup.Pids;
            _memberList = lookup.MemberList;
            _lookup = lookup;
        }

        public async Task ReceiveAsync(IContext context)
        {
            if (context.Message is GetPid msg)
            {
                var pid = await GetPid(msg);
                context.Respond(new PidResult
                {
                    Pid = pid
                });
            }
        }

        private async Task<PID> GetPid(GetPid msg)
        {
            var key = msg.Key;
            var ct = msg.CancellationToken;
            var identity = msg.Identity;
            var kind = msg.Kind;

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
            var requestId = Guid.NewGuid();
            var locked = await TryAcquireLockAsync(key, identity, kind, requestId);
            if (locked)
            {
                //we have the lock, spawn and return
                return await SpawnActivationAsync(key, identity, kind, activator, requestId, ct);
            }

            //we didn't get the lock, spin read for x times before giving up
            return await SpinWaitOnLockAsync(key, identity, kind, ct);
        }

        private async Task<bool> TryAcquireLockAsync(string key, string identity, string kind, Guid requestId)
        {
            var lockEntity = new PidLookupEntity
            {
                Address = null,
                Identity = identity,
                Key = key,
                Kind = kind,
                LockedBy = requestId,
                //Assumes that PLE is deleted when actor is terminated
                Revision = 1
            };
            var l = await _pids.ReplaceOneAsync(x => x.Key == key && x.LockedBy == null && x.Revision == default, lockEntity,
                new ReplaceOptions
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
            Guid requestId, CancellationToken ct)
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

                await _pids.UpdateOneAsync(
                    s => s.Key == key && s.LockedBy == requestId && s.Revision == 1,
                    Builders<PidLookupEntity>.Update
                        .Set(l => l.Address, activator.Address)
                        .Set(l => l.MemberId, activator.Id)
                        .Set(l => l.UniqueIdentity, resp.Pid.Id)
                        .Set(l => l.Revision, 2)
                        .Unset(l => l.LockedBy)
                    , new UpdateOptions(), CancellationToken.None
                );

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
                //There is an active lock on the pid, spinwait
                var i = 0;
                do
                {
                    await Task.Delay(100, ct);
                } while ((pidLookupEntity = await LookupKey(key, ct))?.LockedBy == lockId && ++i < 10);
            }

            if (pidLookupEntity == null) return null;
            if (pidLookupEntity.LockedBy == null)
            {
                return await ValidateAndMapToPid(identity, kind, pidLookupEntity);
            }

            if (pidLookupEntity.LockedBy == lockId)
            {
                //Stale lock. delete?
                _logger.LogWarning($"Stale lock: {pidLookupEntity.Key}");
            }

            //failed to get pid, bail out
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
            if (pidLookup.LockedBy != null)
            {
                //Still initializing..
                return null;
            }
            var memberExists = _memberList.ContainsMemberId(pidLookup.MemberId);
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

        private async Task DeleteLock(string key, Guid requestId, CancellationToken ct)
        {
            await _pids.DeleteOneAsync(x => x.Key == key && x.LockedBy == requestId, ct);
        }
    }
}