// -----------------------------------------------------------------------
// <copyright file="KubernetesPodMetadata.cs" company="Asynkron AB">
//      Copyright (C) 2015-2025 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using static Proto.Cluster.Kubernetes.ProtoLabels;

namespace Proto.Cluster.Kubernetes;

internal static class KubernetesPodMetadata
{
    internal static Dictionary<string, string> ToNonClusterDictionary(IDictionary<string, string> source)
    {
        if (source is null || source.Count == 0)
        {
            return new Dictionary<string, string>();
        }

        var result = new Dictionary<string, string>();

        foreach (var (key, value) in source)
        {
            if (key.StartsWith(ProtoClusterPrefix, StringComparison.Ordinal))
            {
                continue;
            }

            result[key] = value;
        }

        return result;
    }

    internal static void TryAddNonClusterEntries(IDictionary<string, string> source, Dictionary<string, string> destination)
    {
        if (source is null || source.Count == 0)
        {
            return;
        }

        foreach (var (key, value) in source)
        {
            if (key.StartsWith(ProtoClusterPrefix, StringComparison.Ordinal))
            {
                continue;
            }

            destination.TryAdd(key, value);
        }
    }
}
