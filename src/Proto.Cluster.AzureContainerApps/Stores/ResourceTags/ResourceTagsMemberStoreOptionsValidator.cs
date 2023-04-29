using System;
using Microsoft.Extensions.Options;

namespace Proto.Cluster.AzureContainerApps.Stores.ResourceTags;

public class ResourceTagsMemberStoreOptionsValidator : IPostConfigureOptions<ResourceTagsMemberStoreOptions>
{
    public void PostConfigure(string name, ResourceTagsMemberStoreOptions options)
    {
        if (string.IsNullOrEmpty(options.ContainerAppName))
            throw new Exception("No app name provided");

        if (string.IsNullOrEmpty(options.RevisionName))
            throw new Exception("No app revision provided");

        if (string.IsNullOrEmpty(options.ReplicaName))
            throw new Exception("No replica name provided");
    }
}