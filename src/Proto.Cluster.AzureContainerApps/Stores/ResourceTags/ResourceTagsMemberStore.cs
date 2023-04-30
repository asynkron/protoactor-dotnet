using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Azure;
using Azure.ResourceManager;
using Azure.ResourceManager.AppContainers;
using Azure.ResourceManager.Resources;
using Azure.ResourceManager.Resources.Models;
using JetBrains.Annotations;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Proto.Cluster.AzureContainerApps.Stores.ResourceTags;

/// <summary>
/// Stores members in the form of resource tags of the Azure Container Application resource.
/// </summary>
[PublicAPI]
public class ResourceTagsMemberStore : IMemberStore
{
    private readonly ArmClient _armClient;
    private readonly IOptions<ResourceTagsMemberStoreOptions> _options;
    private readonly ILogger _logger;

    public ResourceTagsMemberStore(
        ArmClient armArmClient,
        IOptions<ResourceTagsMemberStoreOptions> options,
        ILogger<ResourceTagsMemberStore> logger)
    {
        _armClient = armArmClient;
        _options = options;
        _logger = logger;
    }

    public async ValueTask<ICollection<Member>> ListAsync(CancellationToken cancellationToken = default)
    {
        var members = new List<Member>();
        var resourceGroupName = _options.Value.ResourceGroupName;
        var containerAppName = _options.Value.ContainerAppName;
        var resourceGroup = await _armClient.GetResourceGroupByName(resourceGroupName).ConfigureAwait(false);
        var containerApp = await resourceGroup.Value.GetContainerAppAsync(containerAppName, cancellationToken).ConfigureAwait(false);

        if (containerApp is null || !containerApp.HasValue)
        {
            _logger.LogError("Container App: {ContainerApp} in resource group: {ResourceGroup} is not found", containerApp, resourceGroupName);
            return members.ToArray();
        }

        var containerAppRevisions = GetActiveRevisionsWithTraffic(containerApp).ToList();
        if (!containerAppRevisions.Any())
        {
            _logger.LogError("Container App: {ContainerApp} in resource group: {ResourceGroup} does not contain any active revisions with traffic", containerAppName, resourceGroupName);
            return members.ToArray();
        }

        var replicasWithTraffic = containerAppRevisions.SelectMany(r => r.GetContainerAppReplicas());
        var allTags = (await containerApp.Value.GetTagResource().GetAsync(cancellationToken).ConfigureAwait(false)).Value.Data.TagValues;

        foreach (var replica in replicasWithTraffic)
        {
            var replicaNameTag = allTags.FirstOrDefault(kvp => kvp.Value == replica.Data.Name);
            if (replicaNameTag.Key == null)
            {
                _logger.LogWarning("Skipping Replica with name: {Name}, no Proto Tags found", replica.Data.Name);
                continue;
            }

            var replicaNameTagPrefix = replicaNameTag.Key.Replace(ResourceTagLabels.LabelReplicaNameWithoutPrefix, string.Empty);
            var currentReplicaTags = allTags.Where(kvp => kvp.Key.StartsWith(replicaNameTagPrefix)).ToDictionary(x => x.Key, x => x.Value);
            var memberId = currentReplicaTags.FirstOrDefault(kvp => kvp.Key.Contains(ResourceTagLabels.LabelMemberIdWithoutPrefix)).Value;

            var kinds = currentReplicaTags
                .Where(kvp => kvp.Key.StartsWith(ResourceTagLabels.LabelKind(memberId)))
                .Select(kvp => kvp.Key[(ResourceTagLabels.LabelKind(memberId).Length + 1)..])
                .ToArray();

            var member = new Member
            {
                Id = currentReplicaTags[ResourceTagLabels.LabelMemberId(memberId)],
                Port = int.Parse(currentReplicaTags[ResourceTagLabels.LabelPort(memberId)]),
                Host = currentReplicaTags[ResourceTagLabels.LabelHost(memberId)],
                Kinds = { kinds }
            };

            members.Add(member);
        }

        return members.ToArray();
    }

    public async ValueTask RegisterAsync(string clusterName, Member member, CancellationToken cancellationToken = default)
    {
        var tags = new Dictionary<string, string>
        {
            [ResourceTagLabels.LabelCluster(member.Id)] = clusterName,
            [ResourceTagLabels.LabelHost(member.Id)] = member.Host,
            [ResourceTagLabels.LabelPort(member.Id)] = member.Port.ToString(),
            [ResourceTagLabels.LabelMemberId(member.Id)] = member.Id,
            [ResourceTagLabels.LabelReplicaName(member.Id)] = _options.Value.ReplicaName
        };

        foreach (var kind in member.Kinds)
        {
            var labelKey = $"{ResourceTagLabels.LabelKind(member.Id)}-{kind}";
            tags.TryAdd(labelKey, "true");
        }

        try
        {
            var resourceGroupName = _options.Value.ResourceGroupName;
            var containerAppName = _options.Value.ContainerAppName;
            await AddMemberTags(resourceGroupName, containerAppName, tags, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception x)
        {
            _logger.LogError(x, "Failed to update metadata");
        }
    }

    public async ValueTask UnregisterAsync(string memberId, CancellationToken cancellationToken = default)
    {
        var resourceGroupName = _options.Value.ResourceGroupName;
        var containerAppName = _options.Value.ContainerAppName;

        var resourceGroup = await _armClient.GetResourceGroupByName(resourceGroupName).ConfigureAwait(false);
        var containerApp = await resourceGroup.Value.GetContainerAppAsync(containerAppName, cancellationToken).ConfigureAwait(false);
        var tagResource = containerApp.Value.GetTagResource();
        var resourceTag = new Tag();
        var existingTags = (await tagResource.GetAsync(cancellationToken).ConfigureAwait(false)).Value.Data.TagValues;

        foreach (var tag in existingTags)
        {
            if (!tag.Key.StartsWith(ResourceTagLabels.LabelPrefix(memberId)))
            {
                resourceTag.TagValues.Add(tag);
            }
        }

        await tagResource.CreateOrUpdateAsync(WaitUntil.Completed, new TagResourceData(resourceTag), cancellationToken).ConfigureAwait(false);
    }

    private async Task AddMemberTags(string resourceGroupName, string containerAppName, Dictionary<string, string> newTags, CancellationToken cancellationToken)
    {
        var resourceTag = new Tag();
        foreach (var tag in newTags)
        {
            resourceTag.TagValues.Add(tag);
        }

        var resourceGroup = await _armClient.GetResourceGroupByName(resourceGroupName).ConfigureAwait(false);
        var containerApp = await resourceGroup.Value.GetContainerAppAsync(containerAppName, cancellationToken: cancellationToken).ConfigureAwait(false);
        var tagResource = containerApp.Value.GetTagResource();
        var existingTags = (await tagResource.GetAsync(cancellationToken).ConfigureAwait(false)).Value.Data.TagValues;

        foreach (var tag in existingTags)
        {
            resourceTag.TagValues.Add(tag);
        }

        await tagResource.CreateOrUpdateAsync(WaitUntil.Completed, new TagResourceData(resourceTag), cancellationToken).ConfigureAwait(false);
    }

    private static IEnumerable<ContainerAppRevisionResource> GetActiveRevisionsWithTraffic(ContainerAppResource containerApp) =>
        containerApp.GetContainerAppRevisions().Where(r => r.HasData && (r.Data.IsActive ?? false) && r.Data.TrafficWeight > 0);
}