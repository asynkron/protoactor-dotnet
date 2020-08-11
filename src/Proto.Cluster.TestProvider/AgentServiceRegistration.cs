using System;
using System.Collections.Generic;

namespace Proto.Cluster.Testing
{
    public class AgentServiceRegistration
    {
        public Guid ID { get; set; }
        public string Address { get; set; }
        public int Port { get; set; }
        public string[] Kinds { get; set; }
    }
}