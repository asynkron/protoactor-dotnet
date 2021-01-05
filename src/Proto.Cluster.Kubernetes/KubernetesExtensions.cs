// -----------------------------------------------------------------------
// <copyright file="KubernetesExtensions.cs" company="Asynkron AB">
//      Copyright (C) 2015-2020 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using k8s;
using k8s.Models;
using Microsoft.AspNetCore.JsonPatch;
using static Proto.Cluster.Kubernetes.ProtoLabels;

// ReSharper disable InvertIf

namespace Proto.Cluster.Kubernetes
{
    static class KubernetesExtensions
    {
        private static string cachedNamespace;

        /// <summary>
        ///     Find the container port for a given pod than matches the given port.
        /// </summary>
        /// <param name="pod">Kubernetes Pod object</param>
        /// <param name="port">Port to find in container ports</param>
        /// <returns></returns>
        internal static V1ContainerPort FindPort(this V1Pod pod, int port)
            => pod.Spec.Containers[0].Ports.FirstOrDefault(x => x.ContainerPort == port);

        /// <summary>
        ///     Replace pod labels
        /// </summary>
        /// <param name="kubernetes">Kubernetes client</param>
        /// <param name="podName">Name of the pod</param>
        /// <param name="podNamespace">Namespace of the pod</param>
        /// <param name="labels">Labels collection. All labels will be replaced by the new labels.</param>
        /// <returns></returns>
        internal static Task<V1Pod> ReplacePodLabels(
            this IKubernetes kubernetes,
            string podName,
            string podNamespace,
            IDictionary<string, string> labels
        )
        {
            var patch = new JsonPatchDocument<V1Pod>();
            patch.Replace(x => x.Metadata.Labels, labels);
            return kubernetes.PatchNamespacedPodAsync(new V1Patch(patch,V1Patch.PatchType.JsonPatch), podName, podNamespace);
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
                .Labels
                .Where(l => l.Key.StartsWith(LabelKind) && l.Value == "true")
                .Select(l => l.Key.Substring(LabelKind.Length + 1))
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
                    Kinds = {kinds}
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
}