using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Proto.Cluster.IdentityLookup;

namespace Proto.Cluster.Partition
{
    public class PartitionIdentityLookup : IIdentityLookup
    {
        private Cluster _cluster = null!;
        private ILogger _logger = null!;
        private PartitionManager _partitionManager = null!;

        public async Task<PID?> GetAsync(string identity, string kind, CancellationToken ct)
        {
            //Get address to node owning this ID
            var address = _partitionManager.Selector.GetIdentityOwner(identity);
            _logger.LogDebug("Identity belongs to {address}", address);

            if (string.IsNullOrEmpty(address))
            {
                return null;
            }

            var remotePid = _partitionManager.RemotePartitionIdentityActor(address);

            var req = new ActivationRequest
            {
                Kind = kind,
                Identity = identity
            };

            _logger.LogDebug("Requesting remote PID from {Partition}:{Remote} {@Request}", address, remotePid, req);

            try
            {
                var resp = ct == CancellationToken.None
                    ? await _cluster.System.Root.RequestAsync<ActivationResponse>(remotePid, req,
                        _cluster.Config!.TimeoutTimespan
                    )
                    : await _cluster.System.Root.RequestAsync<ActivationResponse>(remotePid, req, ct);

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

        public Task SetupAsync(Cluster cluster, string[] kinds, bool isClient)
        {
            _cluster = cluster;
            _partitionManager = new PartitionManager(cluster, isClient);
            _logger = Log.CreateLogger(nameof(PartitionIdentityLookup) + "-" + _cluster.LoggerId);
            _partitionManager.Setup();
            return Task.CompletedTask;
        }

        public Task ShutdownAsync()
        { 
            _partitionManager.Shutdown();
            return Task.CompletedTask;
        }
    }
}