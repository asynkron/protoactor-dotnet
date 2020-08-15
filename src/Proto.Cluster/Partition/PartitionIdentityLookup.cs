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
        private Cluster _cluster = null!;
        private readonly ILogger _logger = Log.CreateLogger<PartitionIdentityLookup>();
        private PartitionManager _partitionManager = null!;

        public async Task<(PID?,ResponseStatusCode)> GetAsync(string identity,string kind, CancellationToken ct)
        {
            //Get address to node owning this ID
            var address = _partitionManager.Selector.GetPartition(identity);
            _logger.LogError("Identity belongs to {address}", address);

            if (string.IsNullOrEmpty(address))
            {
                return (null, ResponseStatusCode.Unavailable);
            }

            var remotePid = _partitionManager.RemotePartitionForKind(address);

            var req = new ActorPidRequest
            {
                Kind = kind,
                Name = identity
            };

            _logger.LogDebug("[Cluster] Requesting remote PID from {Partition}:{Remote} {@Request}", address, remotePid, req);

            try
            {
                var resp = ct == CancellationToken.None
                    ? await _cluster.System.Root.RequestAsync<ActorPidResponse>(remotePid, req, _cluster.Config!.TimeoutTimespan)
                    : await _cluster.System.Root.RequestAsync<ActorPidResponse>(remotePid, req, ct);
                var status = (ResponseStatusCode) resp.StatusCode;

                if (status == ResponseStatusCode.OK && _cluster!.Config!.UsePidCache)
                {
                    if (_cluster.PidCache.TryAddCache(identity, resp.Pid))
                    {
                        _cluster.PidCacheUpdater.Watch(resp.Pid);
                    }
                }

                return (resp.Pid, status);
            }
            catch (TimeoutException e)
            {
                _logger.LogWarning(e, "[Cluster] Remote PID request timeout {@Request}", req);
                return (null, ResponseStatusCode.Timeout);
            }
            catch (Exception e)
            {
                _logger.LogError(e, "[Cluster] Error occured requesting remote PID {@Request}", req);
                return (null, ResponseStatusCode.Error);
            }
        }

        public void Setup(Cluster cluster, string[] kinds)
        {
            _cluster = cluster;
            _partitionManager = new PartitionManager(cluster);
            _partitionManager.Setup();
        }

        public void Shutdown()
        {
            _partitionManager.Stop();
        }
    }
}