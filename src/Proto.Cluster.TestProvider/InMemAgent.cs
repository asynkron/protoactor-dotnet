using System;

namespace Proto.Cluster.Testing
{
    internal class InMemAgent
    {
        public static  object[] GetServicesHealth(string clusterName, QueryOptions queryOptions)
        {
            throw new NotImplementedException();
        }

        public static void RegisterService(AgentServiceRegistration registration)
        {
            throw new NotImplementedException();
        }

        public static void DeregisterService(string id)
        {
            throw new NotImplementedException();
        }

        public static void RefreshServiceTTL(string id)
        {
            throw new NotImplementedException();
        }
    }
}