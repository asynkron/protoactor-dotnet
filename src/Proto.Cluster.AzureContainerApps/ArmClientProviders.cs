using Azure.ResourceManager;
using JetBrains.Annotations;

namespace Proto.Cluster.AzureContainerApps;

/// <summary>
/// Provides an <see cref="ArmClient"/> instance.
/// </summary>
[PublicAPI]
public static class ArmClientProviders
{
    /// <summary>
    /// A default <see cref="IArmClientProvider"/> that uses <see cref="Azure.Identity.DefaultAzureCredential"/>
    /// </summary>
    public static readonly DefaultAzureCredentialArmClientProvider DefaultAzureCredential = new();
}