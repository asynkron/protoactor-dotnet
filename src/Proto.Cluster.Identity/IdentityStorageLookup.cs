// -----------------------------------------------------------------------
// <copyright file="IdentityStorageLookup.cs" company="Asynkron AB">
//      Copyright (C) 2015-2020 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------
using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Proto.Cluster.IdentityLookup;
using Proto.Router;

namespace Proto.Cluster.Identity
{
    public class IdentityStorageLookup : IIdentityLookup
    {
        private const string PlacementActorName = "placement-activator";
        private static readonly int PidClusterIdentityStartIndex = PlacementActorName.Length + 1;
        private readonly ILogger _logger = Log.CreateLogger<IdentityStorageLookup>();
        private bool _isClient;
        private string _memberId;
        private PID _placementActor;
        private PID _worker;
        private ActorSystem _system;
        internal Cluster Cluster;
        internal MemberList MemberList;

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
            _memberId = cluster.Id.ToString();
            MemberList = cluster.MemberList;
            _isClient = isClient;

            var workerProps = Props.FromProducer(() => new IdentityStorageWorker(this));

            _worker = _system.Root.Spawn(workerProps);

            //hook up events
            cluster.System.EventStream.Subscribe<ClusterTopology>(e =>
                {
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

            await Storage.Init();
        }

        public async Task ShutdownAsync()
        {
            if (!_isClient)
            {
                //TODO: rewrite to respond to pending activations
                await Cluster.System.Root.StopAsync(_placementActor);
                try
                {
                    await RemoveMemberAsync(_memberId);
                }
                catch (Exception e)
                {
                    _logger.LogError(e, "Failed to remove stored member activations for {MemberId}", _memberId);
                }
            }
        }

        public Task RemovePidAsync(PID pid, CancellationToken ct) => Storage.RemoveActivation(pid, ct);

        internal Task RemoveMemberAsync(string memberId) => Storage.RemoveMember(memberId, CancellationToken.None);

        internal PID RemotePlacementActor(string address) => PID.FromAddress(address, PlacementActorName);

        public static bool TryGetClusterIdentityShortString(string pidId, out string? clusterIdentity)
        {
            var idIndex = pidId.LastIndexOf("$", StringComparison.Ordinal);
            if (idIndex > PidClusterIdentityStartIndex)
            {
                clusterIdentity = pidId.Substring(PidClusterIdentityStartIndex,
                    idIndex - PidClusterIdentityStartIndex
                );
                return true;
            }

            clusterIdentity = default;
            return false;
        }
    }
}