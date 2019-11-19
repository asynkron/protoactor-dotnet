// -----------------------------------------------------------------------
//   <copyright file="Cluster.cs" company="Asynkron HB">
//       Copyright (C) 2015-2018 Asynkron HB All rights reserved
//   </copyright>
// -----------------------------------------------------------------------

using System;
using Proto.Remote;

namespace Proto.Cluster
{
    public class ClusterConfig
    {
        public string Name { get; }
        public string Address { get; }
        public int Port { get; }
        public IClusterProvider ClusterProvider { get; }

        public RemoteConfig RemoteConfig { get; private set; }
        public TimeSpan TimeoutTimespan { get; private set; }
        public IMemberStatusValue InitialMemberStatusValue { get; private set; }
        public IMemberStatusValueSerializer MemberStatusValueSerializer { get; private set; }
        public Func<string, IMemberStrategy> MemberStrategyBuilder { get; private set; }

        public ClusterConfig(string name, string address, int port, IClusterProvider cp)
        {
            Name = name ?? throw new ArgumentNullException(nameof(name));
            Address = address ?? throw new ArgumentNullException(nameof(address));
            Port = port;
            ClusterProvider = cp ?? throw new ArgumentNullException(nameof(cp));
            
            RemoteConfig = new RemoteConfig();
            TimeoutTimespan = TimeSpan.FromSeconds(5);
            MemberStatusValueSerializer = new NullMemberStatusValueSerializer();
            MemberStrategyBuilder = kind => new SimpleMemberStrategy();
        }

        public ClusterConfig WithRemoteConfig(RemoteConfig remoteConfig)
        {
            RemoteConfig = remoteConfig;
            return this;
        }

        public ClusterConfig WithTimeoutSeconds(int timeoutSeconds)
        {
            TimeoutTimespan = TimeSpan.FromSeconds(timeoutSeconds);
            return this;
        }

        public ClusterConfig WithInitialMemberStatusValue(IMemberStatusValue statusValue)
        {
            InitialMemberStatusValue = statusValue;
            return this;
        }

        public ClusterConfig WithMemberStatusValueSerializer(IMemberStatusValueSerializer serializer)
        {
            MemberStatusValueSerializer = serializer;
            return this;
        }

        public ClusterConfig WithMemberStrategyBuilder(Func<string, IMemberStrategy> builder)
        {
            MemberStrategyBuilder = builder;
            return this;
        }
    }
}