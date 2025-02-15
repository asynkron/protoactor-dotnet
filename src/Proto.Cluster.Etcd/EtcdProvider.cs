using System.Text.Json;
using dotnet_etcd;
using dotnet_etcd.interfaces;
using Etcdserverpb;
using Google.Protobuf;
using Microsoft.Extensions.Logging;
using V3Electionpb;

namespace Proto.Cluster.Etcd;

public class EtcdProvider : IClusterProvider
{
#pragma warning disable CS0618 // Type or member is obsolete
    private static readonly ILogger Logger = Log.CreateLogger<EtcdProvider>();
#pragma warning restore CS0618 // Type or member is obsolete
    private readonly IEtcdClient _client;
    private readonly EtcdProviderConfig _config;
    private Cluster _cluster = null!;
    private CancellationTokenSource _stoppingCts = new();

    private string MemberKey => $"{_config.MembersKeyPrefix}/{_cluster.System.Id}";

    public EtcdProvider(IEtcdClient client, EtcdProviderConfig config)
    {
        _client = client;
        _config = config;
    }

    public async Task StartMemberAsync(Cluster c)
    {
        Logger.LogInformation("Starting etcd provider");

        _cluster = c;
        _stoppingCts = CancellationTokenSource.CreateLinkedTokenSource(_cluster.System.Shutdown);

        var leaseId = await StartLeaseAndKeepAliveTaskAsync();
        await RegisterSystemAsMember(leaseId);
        StartWatchForMemberChangesTask();

        if (_config.MemberElectedHandlers.Count > 0)
            StartCampaignForLeaderTask(leaseId);

        Logger.LogInformation("Started etcd provider");
    }

    public Task StartClientAsync(Cluster c)
    {
        Logger.LogInformation("Starting etcd provider as client");

        _cluster = c;
        _stoppingCts = CancellationTokenSource.CreateLinkedTokenSource(_cluster.System.Shutdown);
        StartWatchForMemberChangesTask();

        Logger.LogInformation("Started etcd provider as client");

        return Task.CompletedTask;
    }

    public async Task ShutdownAsync(bool graceful)
    {
        Logger.LogInformation("Shutting down etcd provider");

        if (graceful)
        {
            await _client.DeleteAsync(MemberKey);
        }

        _stoppingCts.Cancel();

        Logger.LogInformation("Shut down etcd provider");
    }

    private async Task<long> StartLeaseAndKeepAliveTaskAsync()
    {
        var leaseGrantResponse = await _client.LeaseGrantAsync(new LeaseGrantRequest { TTL = _config.LeaseTtl },
            cancellationToken: _stoppingCts.Token);

        _ = SafeTask.Run(async () =>
        {
            await _client.LeaseKeepAlive(leaseGrantResponse.ID, _stoppingCts.Token).ConfigureAwait(false);
            Logger.LogInformation("Lease keep alive stopped");
        }, _stoppingCts.Token);

        return leaseGrantResponse.ID;
    }

    private async Task RegisterSystemAsMember(long leaseId)
    {
        var (host, port) = _cluster.System.GetAddress();
        var kinds = _cluster.GetClusterKinds();
        var memberId = _cluster.System.Id;

        var request = new PutRequest
        {
            Lease = leaseId,
            Key = ByteString.CopyFromUtf8(MemberKey),
            Value = ByteString.CopyFromUtf8(JsonSerializer.Serialize(
                new { id = memberId, host, port, kinds }, _config.JsonSerializerOptions))
        };

        await _client.PutAsync(request, cancellationToken: _cluster.System.Shutdown);

        Logger.LogDebug("Registered local system as member");
    }

    private void StartWatchForMemberChangesTask()
    {
        _ = SafeTask.Run(async () =>
        {
            await _client.WatchRangeAsync(_config.MembersKeyPrefix, (WatchEvent[] _) =>
            {
                Logger.LogDebug("Cluster membership changed");

                var rangeResponse = _client.GetRange(_config.MembersKeyPrefix, cancellationToken: _stoppingCts.Token);
                var members = rangeResponse.Kvs
                    .Select(kv => JsonSerializer.Deserialize<Member>(kv.Value.ToStringUtf8(), _config.JsonSerializerOptions))
                    .Where(m => m != null)
                    .Select(m => m!).ToList();

                _cluster.MemberList.UpdateClusterTopology(members);
            }, cancellationToken: _stoppingCts.Token);

            Logger.LogDebug("Stopped watching for member changes");
        }, _stoppingCts.Token);
    }

    private void StartCampaignForLeaderTask(long leaseId)
    {
        Logger.LogInformation("Start campaigning for leader");

        _ = SafeTask.Run(async () =>
        {
            var campaignRequest = new CampaignRequest
            {
                Name = ByteString.CopyFromUtf8(_config.CampaignKey),
                Value = ByteString.CopyFromUtf8(_cluster.System.Id),
                Lease = leaseId
            };

            var campaignResponse = await _client.CampaignAsync(campaignRequest, cancellationToken: _stoppingCts.Token);
            Logger.LogInformation("Elected as leader with leader key {LeaderKey}", campaignResponse.Leader.Key.ToStringUtf8());

            _config.MemberElectedHandlers.ForEach(h => h(_cluster));
        }, _stoppingCts.Token);
    }
}