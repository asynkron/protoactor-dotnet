using System.Collections.Generic;

namespace Proto.Cluster.Testing
{
    internal class AgentServiceRegistration
    {
        public string ID { get; set; }
        public string Name { get; set; }
        public string[] Tags { get; set; }
        public string Address { get; set; }
        public int Port { get; set; }
        public Dictionary<string, string> Meta { get; set; }
    }
}