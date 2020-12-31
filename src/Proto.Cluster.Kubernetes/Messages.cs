// -----------------------------------------------------------------------
// <copyright file="Messages.cs" company="Asynkron AB">
//      Copyright (C) 2015-2020 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------
using System;

namespace Proto.Cluster.Kubernetes
{
    static class Messages
    {
        public class RegisterMember
        {
            public string ClusterName { get; set; }
            public string Address { get; set; }
            public int Port { get; set; }
            public string[] Kinds { get; set; }
            public string MemberId { get; set; }
        }

        public class DeregisterMember
        {
        }

        public class StartWatchingCluster
        {
            public StartWatchingCluster(string clusterName) => ClusterName = clusterName ?? throw new ArgumentNullException(nameof(clusterName));

            public string ClusterName { get; }
        }

        // public class EnsureWatcher
        // {
        //     public static EnsureWatcher Instance { get; } = new EnsureWatcher();
        // }
    }
}