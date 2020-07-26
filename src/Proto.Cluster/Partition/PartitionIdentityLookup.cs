using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Proto.Cluster.IdentityLookup;
using Proto.Remote;

namespace Proto.Cluster
{
    public class PartitionIdentityLookup : IIdentityLookup
    {
        private Cluster _cluster;
        private static readonly ILogger Logger = Log.CreateLogger<PartitionIdentityLookup>();
        private Partition Partition { get;  set; }

        public async Task<(PID?,ResponseStatusCode)> GetAsync(string identity,string kind, CancellationToken ct)
        {
            //Get Pid
            var address = _cluster.MemberList.GetPartition(identity, kind);

            if (string.IsNullOrEmpty(address))
            {
                return (null, ResponseStatusCode.Unavailable);
            }

            
            var remotePid = Partition.PartitionForKind(address, kind);

            var req = new ActorPidRequest
            {
                Kind = kind,
                Name = identity
            };

            Logger.LogDebug("[Cluster] Requesting remote PID from {Partition}:{Remote} {@Request}", address, remotePid, req);

            try
            {
                var resp = ct == CancellationToken.None
                    ? await _cluster.System.Root.RequestAsync<ActorPidResponse>(remotePid, req, _cluster.Config!.TimeoutTimespan)
                    : await _cluster.System.Root.RequestAsync<ActorPidResponse>(remotePid, req, ct);
                var status = (ResponseStatusCode) resp.StatusCode;

                if (status == ResponseStatusCode.OK)
                {
                    if (_cluster.Config.UsePidCache)
                    {
                        if (_cluster.PidCache.TryAddCache(identity, resp.Pid))
                        {
                            _cluster.PidCacheUpdater.Watch(resp.Pid);
                        }
                    }

                    return (resp.Pid, status);
                }

                return (resp.Pid, status);
            }
            catch (TimeoutException e)
            {
                Logger.LogWarning(e, "[Cluster] Remote PID request timeout {@Request}", req);
                return (null, ResponseStatusCode.Timeout);
            }
            catch (Exception e)
            {
                Logger.LogError(e, "[Cluster] Error occured requesting remote PID {@Request}", req);
                return (null, ResponseStatusCode.Error);
            }
        }

        public void Setup(Cluster cluster, string[] kinds)
        {
            _cluster = cluster;
            Partition = new Partition(cluster);
            Partition.Setup(kinds);
        }

        public void Stop()
        {
            Partition.Stop();
        }
    }
}