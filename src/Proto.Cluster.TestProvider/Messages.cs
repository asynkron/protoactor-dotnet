namespace Proto.Cluster.Testing
{
    internal static class Messages
    {
        public class RegisterMember
        {
            public string ClusterName { get; set; }
            public string Address { get; set; }
            public int Port { get; set; }
            public string[] Kinds { get; set; }
        }

        public class DeregisterMember
        {
        }

        public class UpdateTtl
        {
        }

        public class CheckStatus
        {
            public ulong Index { get; set; }
        }

        public class ReregisterMember
        {
        }
    }
}