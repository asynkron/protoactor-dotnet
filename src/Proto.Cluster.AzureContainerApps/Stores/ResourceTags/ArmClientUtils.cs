using System.Threading.Tasks;
using Azure;
using Azure.ResourceManager;
using Azure.ResourceManager.Resources;

namespace Proto.Cluster.AzureContainerApps.Stores.ResourceTags;

public static class ArmClientUtils
{
    public static async Task<Response<ResourceGroupResource>> GetResourceGroupByName(this ArmClient client, string resourceGroupName) =>
        await (await client.GetDefaultSubscriptionAsync().ConfigureAwait(false)).GetResourceGroups().GetAsync(resourceGroupName).ConfigureAwait(false);
}