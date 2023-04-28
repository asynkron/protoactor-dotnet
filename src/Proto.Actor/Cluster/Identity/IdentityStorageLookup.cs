using System.Threading;
using System.Threading.Tasks;

namespace Proto.Cluster.Identity;

/// <summary>
///     <see cref="IIdentityLookup" /> implementation that uses external database for storing and retrieving identities.
///     See the <a href="https://proto.actor/docs/cluster/db-identity-lookup/">documentation</a> for more information.
/// </summary>
public class IdentityStorageLookup : IIdentityLookup
{
    private const string WorkerActorName = "$identity-storage-worker";
    private const string PlacementActorName = "$placement-activator";
    private bool _isClient;
    private string _memberId = string.Empty;
    private PID _placementActor = null!;
    private ActorSystem _system = null!;
    private PID _worker = null!;
    internal Cluster Cluster = null!;
    internal MemberList MemberList = null!;

    public IdentityStorageLookup(IIdentityStorage storage)
    {
        Storage = storage;
    }

    internal IIdentityStorage Storage { get; }

    public async Task<PID?> GetAsync(ClusterIdentity clusterIdentity, CancellationToken ct)
    {
        var msg = new GetPid(clusterIdentity, ct);

        var res = await _system.Root.RequestAsync<PidResult>(_worker, msg, ct).ConfigureAwait(false);

        if (res?.IdentityBlocked == true)
        {
            throw new IdentityIsBlockedException(clusterIdentity);
        }

        return res?.Pid;
    }

    public async Task SetupAsync(Cluster cluster, string[] kinds, bool isClient)
    {
        Cluster = cluster;
        _system = cluster.System;
        _memberId = cluster.System.Id;
        MemberList = cluster.MemberList;
        _isClient = isClient;
        await Storage.Init().ConfigureAwait(false);

        var workerProps = Props.FromProducer(() => new IdentityStorageWorker(this));
        _worker = _system.Root.SpawnNamedSystem(workerProps, WorkerActorName);

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

        if (isClient)
        {
            return;
        }

        var props = Props.FromProducer(() => new IdentityStoragePlacementActor(Cluster, this));
        _placementActor = _system.Root.SpawnNamedSystem(props, PlacementActorName);
    }

    public async Task ShutdownAsync()
    {
        await Cluster.System.Root.StopAsync(_worker).ConfigureAwait(false);

        if (!_isClient)
        {
            await Cluster.System.Root.StopAsync(_placementActor).ConfigureAwait(false);
        }

        await RemoveMemberAsync(_memberId).ConfigureAwait(false);
    }

    public Task RemovePidAsync(ClusterIdentity clusterIdentity, PID pid, CancellationToken ct)
    {
        if (_system.Shutdown.IsCancellationRequested)
        {
            return Task.CompletedTask;
        }

        return Storage.RemoveActivation(clusterIdentity, pid, ct);
    }

    internal Task RemoveMemberAsync(string memberId) => Storage.RemoveMember(memberId, CancellationToken.None);

    internal PID RemotePlacementActor(string address) => PID.FromAddress(address, PlacementActorName);
}