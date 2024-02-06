using System;
using System.Linq;
using System.Threading.Tasks;
using JetBrains.Annotations;
using k8s;
using k8s.Models;

namespace Proto.Cluster.Kubernetes;

public static class KubernetesHelper
{
    /// <summary>
    /// Checks if the Kubernetes namespace file exists. 
    /// </summary>
    [PublicAPI]
    public static bool HasKubeNamespace() => KubernetesExtensions.TryGetKubeNamespace(out _);
    
    /// <summary>
    /// Attempts to get the FQDN for the Pod by querying the Kubernetes API.
    /// </summary>
    /// <exception cref="ApplicationException">Unable to detected k8s or if Subdomain is missing from Pod Definition.</exception>
    [PublicAPI]
    public static async ValueTask<string> GetPodFqdn(this KubernetesProvider kubernetesProvider)
    {
        return await GetPodFqdn(kubernetesProvider.Config);
    }

    /// <summary>
    /// Attempts to get the FQDN for the Pod by querying the Kubernetes API.
    /// </summary>
    /// <exception cref="ApplicationException">Unable to detected k8s or if Subdomain is missing from Pod Definition.</exception>
    [PublicAPI]
    public static async ValueTask<string> GetPodFqdn(this KubernetesProviderConfig config)
    {
        var kubeNamespace = KubernetesExtensions.GetKubeNamespace();

        var k8SClient = config.ClientFactory();
        var pod = await k8SClient.CoreV1.ReadNamespacedPodAsync(Environment.MachineName, kubeNamespace)
            .ConfigureAwait(false);

        if (string.IsNullOrEmpty(pod.Spec.Subdomain))
        {
            throw new ApplicationException("Failed to get the FQDN for the Pod, the spec.subdomain or spec.serviceName is not set");
        }

        // Look up our workload kind, and then determine if we are a pod with a stable name and so can use our host name
        // of if we are a workload like a Deployment, and need to use our IP address for the FQDN.

        var ownerReferences = pod.Metadata.OwnerReferences;
        var isStableHostnameWorkload = ownerReferences.Any(
            // Add other workload kinds with stable hostnames as needed
            owner => owner.Kind is "StatefulSet" or "DaemonSet" 
        );

        var host = isStableHostnameWorkload ? pod.Metadata.Name : pod.Status.PodIP.Replace('.', '-');
        var hostFqdn = $"{host}.{pod.Spec.Subdomain}.{pod.Namespace()}.svc.{config.ClusterDomain}";
        return hostFqdn;
    }
}