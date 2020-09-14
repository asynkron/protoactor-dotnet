using System;

namespace Proto.Cluster.Kubernetes
{
    static class Messages
    {
        public class RegisterMember
        {
            public string   ClusterName   { get; set; }
            public string   Address       { get; set; }
            public int      Port          { get; set; }
            public string[] Kinds         { get; set; }
        }

        public class DeregisterMember { }

        public class StartWatchingCluster
        {
            public string ClusterName { get; }

            public StartWatchingCluster(string clusterName) => ClusterName = clusterName ?? throw new ArgumentNullException(nameof(clusterName));
        }

        public class ReregisterMember { }

        // public class EnsureWatcher
        // {
        //     public static EnsureWatcher Instance { get; } = new EnsureWatcher();
        // }
    }
}