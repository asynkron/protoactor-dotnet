// -----------------------------------------------------------------------
//   <copyright file="ConsulProvider.cs" company="Asynkron HB">
//       Copyright (C) 2015-2018 Asynkron HB All rights reserved
//   </copyright>
// -----------------------------------------------------------------------

using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Timer = System.Timers.Timer;

namespace Proto.Cluster.Testing
{
    public class TestProvider : IClusterProvider
    {
        private readonly TestProviderOptions _options;
        private Timer _ttlReportTimer;
        private string _id;
        private string _clusterName;
        private ActorSystem _system;
        private static readonly ILogger Logger = Log.CreateLogger<TestProvider>();


        public TestProvider(TestProviderOptions options)
        {
            _options = options;
        }
        

        public Task RegisterMemberAsync(Cluster cluster,
            string clusterName, string address, int port, string[] kinds, IMemberStatusValue statusValue,
            IMemberStatusValueSerializer statusValueSerializer
        )
        {
            _id = $"{clusterName}@{address}:{port}";
            _clusterName = clusterName;
            _system = cluster.System;

            StartTTLTimer();
            
            return Actor.Done;
        }

        private async Task NotifyStatuses(ulong index)
        {
            //TODO: how do we query the inmem store fore changes? RX?
            var statuses = InMemAgent.GetServicesHealth(
                _clusterName, new QueryOptions
                {
                    WaitIndex = index,
                    WaitTime = _options.BlockingWaitTime
                }
            );

            Logger.LogDebug("TestAgent response: {@Response}", (object) statuses);

            // var memberStatuses =
            //     statuses.Select(
            //             x => new MemberStatus(
            //                 x.Service.ID, x.Service.Address, x.Service.Port, x.Service.Tags,
            //                 x.Checks.All(c => c.Status.Status != "critical"),
            //                 _statusValueSerializer.Deserialize(x.Service.Meta["StatusValue"])
            //             )
            //         )
            //         .ToList();


       //     var res = new ClusterTopologyEvent(memberStatuses);
         //   _system.EventStream.Publish(res);
        }

        private void StartTTLTimer()
        {
            _ttlReportTimer = new Timer(_options.RefreshTtl.TotalMilliseconds);
            _ttlReportTimer.Elapsed += (sender, args) => { RefreshTTL(); };
            _ttlReportTimer.Enabled = true;
            _ttlReportTimer.AutoReset = true;
            _ttlReportTimer.Start();
        }

        private void RefreshTTL()
        {
            InMemAgent.RefreshServiceTTL(_id);
        }

        public Task DeregisterMemberAsync(Cluster cluster)
        {
            Logger.LogDebug("Unregistering service {Service}", _id);

            _ttlReportTimer.Stop();
            InMemAgent.DeregisterService(_id);
            return Task.CompletedTask;
        }

        public Task Shutdown(Cluster cluster)
        {
            return DeregisterMemberAsync(cluster);
        }

        public void MonitorMemberStatusChanges(Cluster cluster)
        {
            //TODO start listening to status changes
            
        }

        public Task UpdateMemberStatusValueAsync(Cluster cluster, IMemberStatusValue statusValue)
        {
            //who calls this?
            //cluster.System.Root.Send(_clusterMonitor, new Messages.UpdateStatusValue { StatusValue = statusValue });
            return Actor.Done;
        }
    }
}