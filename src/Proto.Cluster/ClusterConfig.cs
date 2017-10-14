﻿// -----------------------------------------------------------------------
//   <copyright file="Cluster.cs" company="Asynkron HB">
//       Copyright (C) 2015-2017 Asynkron HB All rights reserved
//   </copyright>
// -----------------------------------------------------------------------

using System;

namespace Proto.Cluster
{
    public class ClusterConfig
    {
        public string Name { get; }
        public string Address { get; }
        public int Port { get; }
        public IClusterProvider ClusterProvider { get; }
        public IMemberStatusValue InitialMemberStatusValue { get; private set; }
        public IMemberStrategyProvider MemberStrategyProvider { get; private set; }
        public IMemberStatusValueSerializer MemberStatusValueSerializer { get; private set; }

        public ClusterConfig(string name, string address, int port, IClusterProvider cp)
        {
            Name = name ?? throw new ArgumentNullException(nameof(name));
            Address = address ?? throw new ArgumentNullException(nameof(address));
            Port = port;
            ClusterProvider = cp ?? throw new ArgumentNullException(nameof(cp));

            InitialMemberStatusValue = MemberStatusValue.DefaultValue;
            MemberStrategyProvider = new MemberStrategyProvider();
            MemberStatusValueSerializer = new MemberStatusValueSerializer();
        }

        public ClusterConfig WithInitialMemberStatusValue(IMemberStatusValue statusValue)
        {
            InitialMemberStatusValue = statusValue;
            return this;
        }

        public ClusterConfig WithMemberStrategyProvider(IMemberStrategyProvider provider)
        {
            MemberStrategyProvider = provider;
            return this;
        }

        public ClusterConfig WithMemberStatusValueSerializer(IMemberStatusValueSerializer serializer)
        {
            MemberStatusValueSerializer = serializer;
            return this;
        }
    }
}