// -----------------------------------------------------------------------
// <copyright file="KubernetesExtensions.cs" company="Asynkron AB">
//      Copyright (C) 2015-2025 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using JetBrains.Annotations;
using Json.Patch;
using k8s;
using k8s.Models;
using static Proto.Cluster.Kubernetes.ProtoLabels;

// ReSharper disable InvertIf

namespace Proto.Cluster.Kubernetes;

internal static class KubernetesExtensions
{
    private static string cachedNamespace;

    /// <summary>
    ///     Replace pod labels
    /// </summary>
    /// <param name="kubernetes">Kubernetes client</param>
    /// <param name="podName">Name of the pod</param>
    /// <param name="podNamespace">Namespace of the pod</param>
    /// <param name="pod">the pod that should be patched</param>
    /// <param name="labels">Labels collection. All labels will be replaced by the new labels.</param>
    /// <param name="annotations">annotations collection. All annotations will be replaced by the new annotations.</param>
    /// <returns></returns>
    internal static Task<V1Pod> ReplacePodLabelsAndAnnotations(
        this IKubernetes kubernetes,
        string podName,
        string podNamespace,
        V1Pod pod,
        IDictionary<string, string> labels,
        IDictionary<string, string> annotations
    )
    {
        var old = JsonSerializer.SerializeToDocument(pod);
        pod.Metadata.Labels = labels;
        pod.Metadata.Annotations = annotations;
        var expected = JsonSerializer.SerializeToDocument(pod);
        var patch = old.CreatePatch(expected);

        return kubernetes.CoreV1.PatchNamespacedPodAsync(new V1Patch(patch, V1Patch.PatchType.JsonPatch), podName,
            podNamespace);
    }

    /// <summary>
    ///     Get the pod status. The pod must be running in order to be considered as a candidate.
    /// </summary>
    /// <param name="pod">Kubernetes Pod object</param>
    /// <param name="config">Kubernetes provider configuration</param>
    /// <returns></returns>
    [CanBeNull]
    internal static MemberStatus GetMemberStatus(this V1Pod pod, KubernetesProviderConfig config)
    {
        if (pod.Metadata?.DeletionTimestamp is not null)
        {
            return null;
        }

        var isRunning = pod.Status is { Phase: "Running", PodIP: not null };

        var containerStatuses = pod.Status?.ContainerStatuses;
        if (containerStatuses is null)
        {
            return null;
        }

        var labels = pod.Metadata?.Labels;
        if (labels is null)
        {
            return null;
        }

        if (!labels.TryGetValue(LabelPort, out var portLabel) ||
            !int.TryParse(portLabel, NumberStyles.Integer, CultureInfo.InvariantCulture, out var port))
        {
            return null;
        }

        if (!labels.TryGetValue(LabelMemberId, out var memberId))
        {
            return null;
        }

        var annotations = pod.Metadata.Annotations;
        if (annotations is null || !annotations.TryGetValue(AnnotationKinds, out var kindsAnnotation))
        {
            return null;
        }

        var kinds = kindsAnnotation.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        var host = pod.Status?.PodIP ?? "";
        if (labels.TryGetValue(LabelHost, out var hostOverride) && !string.IsNullOrWhiteSpace(hostOverride))
        {
            host = hostOverride;
        }
        else if (labels.TryGetValue(LabelHostPrefix, out var hostPrefix) && !string.IsNullOrWhiteSpace(hostPrefix))
        {
            var dnsPostfix = $".{pod.Namespace()}.svc.{config.ClusterDomain}";

            // If we have a subdomain, then we can add that to the dnsPostfix, as it will be known to the cluster
            if (!string.IsNullOrEmpty(pod.Spec?.Subdomain))
            {
                dnsPostfix = $".{pod.Spec.Subdomain}{dnsPostfix}";
            }

            host = hostPrefix + dnsPostfix;
        }

        var alive = containerStatuses.All(x => x.Ready);

        return new MemberStatus(isRunning, alive,
            new Member
            {
                Id = memberId,
                Host = host,
                Port = port,
                Kinds = { kinds }
            });
    }

    /// <summary>
    ///     Get the namespace of the current pod
    /// </summary>
    /// <returns>The pod namespace</returns>
    /// <exception cref="InvalidOperationException">Failed to get the Kubernetes namespace, not running in a k8s cluster</exception>
    internal static string GetKubeNamespace()
    {
        if (TryGetKubeNamespace(out var kubeNamespace))
        {
            return kubeNamespace;
        }

        throw new InvalidOperationException("The application doesn't seem to be running in Kubernetes");
    }

    internal static bool TryGetKubeNamespace(out string kubeNamespace)
    {
        if (cachedNamespace is not null)
        {
            if (cachedNamespace.Length == 0)
            {
                // We have already tried to get the namespace, and it was not found.
                kubeNamespace = null;
                return false;
            }
            
            kubeNamespace = cachedNamespace;
            return true;
        }

        var namespaceFile = Path.Combine(
            $"{Path.DirectorySeparatorChar}var",
            "run",
            "secrets",
            "kubernetes.io",
            "serviceaccount",
            "namespace"
        );

        if (!File.Exists(namespaceFile))
        {
            // Note setting it to empty, so we don't try again.
            kubeNamespace = null;
            return false;
        }
        
        // k8s has a limit of 63 characters for namespace names 
        // Limit to reading 63 characters, in case a larger files is there, we will just ignore it.
        using var reader = new StreamReader(namespaceFile, Encoding.UTF8);
        var buffer = new char[65];
        var read = reader.Read(buffer, 0, 64);
        if (read == 0 || read > 63)
        {
            cachedNamespace = string.Empty;
            kubeNamespace = null;
            return false;
        }
        
        kubeNamespace = cachedNamespace = new string(buffer, 0, read).Trim();
        if (string.IsNullOrWhiteSpace(kubeNamespace))
        {
            // Set it to something, so we know we read it, and we don't try again.
            cachedNamespace = string.Empty;
            kubeNamespace = null;
            return false;
        }

        return true;
    }

    /// <summary>
    ///     A wrapper about getting the machine name. The pod name is always the "machine" name/
    /// </summary>
    /// <returns></returns>
    internal static string GetPodName() => Environment.MachineName;
}

public record MemberStatus(bool IsRunning, bool IsReady, Member Member);
