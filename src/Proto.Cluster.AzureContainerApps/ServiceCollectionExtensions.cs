using System;
using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.DependencyInjection;
using Proto.Cluster.AzureContainerApps.Stores.ResourceTags;

namespace Proto.Cluster.AzureContainerApps;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddAzureContainerAppsProvider(this IServiceCollection services, [AllowNull]Action<AzureContainerAppsProviderOptions> configure = null)
    {
        var configureOptions = configure ?? (_ => { });
        services.Configure(configureOptions);
        services.AddSingleton<AzureContainerAppsProvider>();
        return services;
    }
}