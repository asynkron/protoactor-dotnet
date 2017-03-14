using System;
using System.Collections.Generic;
using System.Text;

namespace Proto.Cluster
{
    public interface IClusterProvider
    {
        void RegisterMember(string clusterName, string h, int p, string[] kinds);
        void MonitorMemberStatusChanges();
    }
}
