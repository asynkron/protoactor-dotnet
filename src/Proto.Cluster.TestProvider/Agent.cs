using System;
using System.Threading.Tasks;

namespace Proto.Cluster.Consul
{
    internal class Agent
    {
        public static async Task<> ServiceHealth(string clusterName, QueryOptions queryOptions)
        {
            throw new NotImplementedException();
        }

        public static Task ServiceRegister(AgentServiceRegistration registration)
        {
            throw new NotImplementedException();
        }

        public static Task ServiceDeregister(string id)
        {
            throw new NotImplementedException();
        }
    }
}