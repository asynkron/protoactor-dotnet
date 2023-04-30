using System;
using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.DependencyInjection;

namespace Proto.Cluster.AzureContainerApps.Stores.ResourceTags;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddResourceTagsMemberStore(this IServiceCollection services, [AllowNull]Action<ResourceTagsMemberStoreOptions> configure = null)
    {
        var configureOptions = configure ?? (_ => { });
        services.Configure(configureOptions);

        services.AddSingleton<IMemberStore, ResourceTagsMemberStore>();

        return services;
    }
}