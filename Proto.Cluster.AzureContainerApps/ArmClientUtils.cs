using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Azure;
using Azure.Core;
using Azure.ResourceManager;
using Azure.ResourceManager.AppContainers;
using Azure.ResourceManager.Resources;
using Azure.ResourceManager.Resources.Models;
using Microsoft.Extensions.Logging;

namespace Proto.Cluster.AzureContainerApps;

public static class ArmClientUtils
{
    private static readonly ILogger Logger = Log.CreateLogger(nameof(ArmClientUtils));

    public static async Task<Member[]> GetClusterMembers(this ArmClient client, ResourceIdentifier resourceGroup, string containerAppName)
    {
        var members = new List<Member>();
        
        var containerApp = await client.GetResourceGroupResource(resourceGroup).GetContainerAppAsync(containerAppName);

        if (containerApp is null || !containerApp.HasValue)
        {
            Logger.LogError("Container App: {ContainerApp} in resource group: {ResourceGroup} is not found", containerApp, resourceGroup);
            return members.ToArray();
        }

        var containerAppRevisions = GetActiveRevisionsWithTraffic(containerApp);
        
        Logger.LogError("Container App: {ContainerApp} in resource group: {ResourceGroup} does not contain any active revisions with traffic", containerAppName, resourceGroup);
        
        foreach (var revision in containerAppRevisions)
        {
            foreach (var replica in revision.GetContainerAppReplicas())
            {
                var tags = replica.GetTagResource().Data.TagValues;

                if (!tags.ContainsKey(ProtoLabels.LabelMemberId))
                {
                    Logger.LogWarning("Skipping Replica {Id}, no Proto Tags found", replica.Data.Id);
                    continue;
                }

                var kinds = tags
                    .Where(kvp => kvp.Key.StartsWith(ProtoLabels.LabelKind))
                    .Select(kvp => kvp.Key[(ProtoLabels.LabelKind.Length + 1)..])
                    .ToArray();

                var member = new Member
                {
                    Id = tags[ProtoLabels.LabelMemberId],
                    Port = int.Parse(tags[ProtoLabels.LabelPort]),
                    Host = revision.Data.Template.Containers.First().Probes.First().HttpRequest.Host,
                    Kinds = { kinds }
                };

                members.Add(member);
            }
        }

        return members.ToArray();
    }

    public static async Task UpdateMemberMetadata(this ArmClient client, ResourceIdentifier resourceGroup, string containerAppName, string revisionName, string replicaName, Dictionary<string, string> tags)
    {
        var resourceTag = new Tag();
        foreach (var tag in tags)
        {
            resourceTag.TagValues.Add(tag);
        }

        var containerApp = await client.GetResourceGroupResource(resourceGroup).GetContainerAppAsync(containerAppName);
        var revision = await containerApp.Value.GetContainerAppRevisionAsync(revisionName);
        var replica = await revision.Value.GetContainerAppReplicaAsync(replicaName);
        await replica.Value.GetTagResource().CreateOrUpdateAsync(WaitUntil.Completed, new TagResourceData(resourceTag));
    }
    
    private static IEnumerable<ContainerAppRevisionResource> GetActiveRevisionsWithTraffic(ContainerAppResource containerApp)
    {
        return containerApp.GetContainerAppRevisions()
            .Where(r => r.HasData && r.Data.Active.GetValueOrDefault(false) && r.Data.TrafficWeight > 0);
    }
}