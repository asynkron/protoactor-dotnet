using System.Text.Json;
using System.Text.Json.Serialization;

namespace Proto.Cluster.Etcd;

public record EtcdProviderConfig
{
    public int LeaseTtl { get; init; } = 5;

    public string MembersKeyPrefix { get; init; } = "cluster/members";

    public string CampaignKey { get; init; } = "cluster/leader";

    public string LeaderGossipKey { get; init; } = "cluster:leader";

    public JsonSerializerOptions? JsonSerializerOptions { get; init; } = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        NumberHandling = JsonNumberHandling.AllowReadingFromString
    };

    public List<Action<Cluster>> MemberElectedHandlers = new();

    public EtcdProviderConfig WithLeaseTtl(int leaseTtl) => this with { LeaseTtl = leaseTtl };

    public EtcdProviderConfig WithMembersKeyPrefix(string prefix) => this with { MembersKeyPrefix = prefix };

    public EtcdProviderConfig WithCampaignKey(string key) => this with { CampaignKey = key };

    public EtcdProviderConfig WithLeaderGossipKey(string key) => this with { LeaderGossipKey = key };

    public EtcdProviderConfig WithJsonSerializerOptions(JsonSerializerOptions options) => this with { JsonSerializerOptions = options };

    public EtcdProviderConfig WithElectedCallback(Action<Cluster> handler)
    {
        MemberElectedHandlers.Add(handler);
        return this;
    }
}