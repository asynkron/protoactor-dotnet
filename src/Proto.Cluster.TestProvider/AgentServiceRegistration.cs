using System;

namespace Proto.Cluster.Testing
{
    public class AgentServiceRegistration
    {
        public Guid ID { get; set; }
        public string Host { get; set; }
        public int Port { get; set; }
        public string[] Kinds { get; set; }
    }
}