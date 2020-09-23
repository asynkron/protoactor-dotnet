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
            
            var pidLookup = await _pids.Find(x => x.Key == key).Limit(1).SingleOrDefaultAsync(ct);
            if (pidLookup != null)
            {
                var pid = new PID(pidLookup.Address, pidLookup.UniqueIdentity);
                var memberExists = _memberList.ContainsMemberId(pidLookup.MemberId);
                if (memberExists) return pid;
                
                _logger.LogDebug("Found placement lookup for {Identity} {Kind}, but Member {MemberId} is not part of cluster",identity,kind,pidLookup.MemberId);
                //if not, spawn a new actor and replace entry
            }

            var activator = _memberList.GetActivator(kind);
            if (activator == null)
            {
                return null;
            }
            
            //TODO: acquire global lock here.

            var requestId = Guid.NewGuid();
            var lockEntity = new PidLookupEntity
            {
                Address = null,
                Identity = identity,
                Key = key,
                Kind = kind,
                LockedBy = requestId
            };
            //write to mongo, use filter for if lockedby is null
            //if no suck document was found, go into spinwait
            //if updated, we now own the lock
            
            //TODO: create the impl :)
            
            _logger.LogDebug("Storing placement lookup for {Identity} {Kind}",identity,kind);
            
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

                var entry = new PidLookupEntity
                {
                    Address = activator.Address,
                    Identity = identity,
                    UniqueIdentity = resp.Pid.Id,
                    Key = key,
                    Kind = kind,
                    MemberId = activator.Id
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
    }
}