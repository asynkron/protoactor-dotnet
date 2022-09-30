// -----------------------------------------------------------------------
// <copyright file="KubernetesExtensions.cs" company="Asynkron AB">
//      Copyright (C) 2015-2022 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
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

        return kubernetes.PatchNamespacedPodAsync(new V1Patch(patch, V1Patch.PatchType.JsonPatch), podName,
            podNamespace);
    }

    /// <summary>
    ///     Get the pod status. The pod must be running in order to be considered as a candidate.
    /// </summary>
    /// <param name="pod">Kubernetes Pod object</param>
    /// <returns></returns>
    internal static (bool IsCandidate, bool IsAlive, Member Status) GetMemberStatus(this V1Pod pod)
    {
        var isCandidate = pod.Status.Phase == "Running" && pod.Status.PodIP is not null;

        var kinds = pod
            .Metadata
            .Annotations
            .Where(l => l.Key.StartsWith(AnnotationKinds))
            .SelectMany(l => l.Value.Split(';'))
            .ToArray();

        var host = pod.Status.PodIP ?? "";
        var port = Convert.ToInt32(pod.Metadata.Labels[LabelPort]);
        var mid = pod.Metadata.Labels[LabelMemberId];
        var alive = pod.Status.ContainerStatuses.All(x => x.Ready);

        return (isCandidate, alive,
            new Member
            {
                Id = mid,
                Host = host,
                Port = port,
                Kinds = { kinds }
            });
    }

    /// <summary>
    ///     Get the namespace of the current pod
    /// </summary>
    /// <returns>The pod namespace</returns>
    internal static string GetKubeNamespace()
    {
        if (cachedNamespace is null)
        {
            var namespaceFile = Path.Combine(
                $"{Path.DirectorySeparatorChar}var",
                "run",
                "secrets",
                "kubernetes.io",
                "serviceaccount",
                "namespace"
            );

            cachedNamespace = File.ReadAllText(namespaceFile);
        }

        return cachedNamespace;
    }

    /// <summary>
    ///     A wrapper about getting the machine name. The pod name is always the "machine" name/
    /// </summary>
    /// <returns></returns>
    internal static string GetPodName() => Environment.MachineName;
}