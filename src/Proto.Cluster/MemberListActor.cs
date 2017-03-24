using System;
using System.Collections.Generic;
using System.Net.NetworkInformation;
using System.Text;
using System.Threading.Tasks;

namespace Proto.Cluster
{
    public class MemberListActor : IActor
    {
        private readonly Dictionary<string,MemberStatus> _members = new Dictionary<string, MemberStatus>();
        public Task ReceiveAsync(IContext context)
        {
            switch (context.Message)
            {
                case ClusterTopologyEvent msg:

                    var tmp = new Dictionary<string,MemberStatus>();
                    foreach (var status in msg.Statuses)
                    {
                        var address = status.Address;
                        tmp[address] = status;
                    }

                    foreach (var kvp in _members)
                    {
                        if (!tmp.TryGetValue(kvp.Key, out var _))
                        {
                            Notify(kvp.Key, null, kvp.Value);
                        }
                    }

                    foreach (var kvp in tmp)
                    {
                        if (!_members.TryGetValue(kvp.Key, out var _))
                        {
                            _members[kvp.Key] = kvp.Value;
                            Notify(kvp.Key, kvp.Value,null);
                        }
                    }

                    break;
                default:
                    break;
            }
            return Actor.Done;
        }

        private void Notify(string address, MemberStatus newStatus, MemberStatus oldStatus)
        {
            if (newStatus == null && oldStatus == null)
            {
                return; //ignore
            }

            if (newStatus == null)
            {
                //notify left

                return;
            }


        }
    }
}
