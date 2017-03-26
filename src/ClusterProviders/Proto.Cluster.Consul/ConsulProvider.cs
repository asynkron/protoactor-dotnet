// -----------------------------------------------------------------------
//   <copyright file="ConsulProvider.cs" company="Asynkron HB">
//       Copyright (C) 2015-2017 Asynkron HB All rights reserved
//   </copyright>
// -----------------------------------------------------------------------

using System;
using Consul;

namespace Proto.Cluster.Consul
{
    public class ConsulProvider : IClusterProvider
    {
        private TimeSpan _blockingWaitTime;
        private string _clusterName;
        private TimeSpan _deregisterCritical;
        private string _id;
        private ulong _index;
        private TimeSpan _refreshTTL;
        private TimeSpan _TTL;


        public void RegisterMember(string clusterName, string host, int port, string[] kinds)
        {
            _id = $"{clusterName}@{host}:{port}";
            _clusterName = clusterName;
            _index = 0;
            _TTL = TimeSpan.FromSeconds(3);
            _refreshTTL = TimeSpan.FromSeconds(1);
            _deregisterCritical= TimeSpan.FromSeconds(10);
            _blockingWaitTime = TimeSpan.FromSeconds(20);

            var client = new ConsulClient(c =>
            {
               //TODO: config
            });

            var s = new AgentServiceRegistration
            {
                ID = _id,
                Name = clusterName,
                Tags = kinds,
                Address = host,
                Port = port,
                Check = new AgentServiceCheck
                        {
                            DeregisterCriticalServiceAfter = _deregisterCritical,
                            TTL = _TTL
                        }
            };
            client.Agent.ServiceRegister(s);
            
        }

        public void MonitorMemberStatusChanges()
        {
            throw new NotImplementedException();
        }
    }
}
