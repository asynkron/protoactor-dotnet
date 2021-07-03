// -----------------------------------------------------------------------
// <copyright file="AmazonEcsClusterMonitor.cs" company="Asynkron AB">
//      Copyright (C) 2015-2020 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using static Proto.Cluster.AmazonECS.Messages;
using static Proto.Cluster.AmazonECS.ProtoLabels;

namespace Proto.Cluster.AmazonECS
{
    class AmazonEcsClusterMonitor : IActor
    {
        private static readonly ILogger Logger = Log.CreateLogger<AmazonEcsClusterMonitor>();
        private readonly Cluster _cluster;

        private string _address;

        private string _clusterName;
        private string _podName;
        private bool _stopping;

        private bool _watching;
        private readonly AmazonEcsProviderConfig _config;

        public AmazonEcsClusterMonitor(Cluster cluster, AmazonEcsProviderConfig config)
        {
            _cluster = cluster;
           
            _config = config;
        }

        public Task ReceiveAsync(IContext context) => context.Message switch
        {
            RegisterMember cmd       => Register(cmd),
            StartWatchingCluster cmd => StartWatchingCluster(cmd.ClusterName),
            DeregisterMember         => StopWatchingCluster(),
            Stopping                 => StopWatchingCluster(),
            _                        => Task.CompletedTask
        };

        private Task Register(RegisterMember cmd)
        {
            _clusterName = cmd.ClusterName;
            _address = cmd.Address;
            
            return Task.CompletedTask;
        }

        private Task StopWatchingCluster()
        {
           
            return Task.CompletedTask;
        }
        
        

        private Task StartWatchingCluster(string clusterName)
        {
            
            var selector = $"{LabelCluster}={clusterName}";
            
            Logger.Log(_config.DebugLogLevel, "[Cluster][AmazonEcsProvider] Starting to watch pods with {Selector}", selector);

            return Task.CompletedTask;
        }

        
    }
}