// -----------------------------------------------------------------------
// <copyright file="RendezvousBenchmark.cs" company="Asynkron AB">
//      Copyright (C) 2015-2021 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------
using System;
using System.Linq;
using BenchmarkDotNet.Attributes;
using Proto;
using Proto.Cluster;
using Proto.Cluster.Partition;
using Proto.Router;

namespace ClusterMicroBenchmarks
{
    [MemoryDiagnoser, InProcess]

    public class RendezvousBenchmark
    {
        [Params(2,10,100)]
        public int NodeCount { get; set; }

        private int _i = 0;
        private static readonly string[] Ids = Enumerable.Range(0, 100).Select(_ => Guid.NewGuid().ToString()).ToArray();

        private Rendezvous _rendezvous;
        private HashRing<Member> _hashRing;
        private HashRing2<Member> _hashRing2;

        [GlobalSetup]
        public void Setup()
        {
            _rendezvous = new Rendezvous();
            var members = Enumerable.Range(0,NodeCount).Select(i => new Member
            {
                Host = "localhost",
                Id = Guid.NewGuid().ToString("N"),
                Port = i + 1000
            }).ToArray();
            _rendezvous.UpdateMembers(members);
            _hashRing = new HashRing<Member>(members, member => member.Address, MurmurHash2.Hash, 100);
            _hashRing2 = new HashRing2<Member>(members, member => member.Address, MurmurHash2.Hash, 100);
        }

        [Benchmark]
        public void Rendezvous()
        {
            var id = Ids[_i++ % Ids.Length];
            var owner = _rendezvous.GetOwnerMemberByIdentity(id);
        }
        
        [Benchmark]
        public void HashRing()
        {
            var id = Ids[_i++ % Ids.Length];
            var owner = _hashRing.GetNode(id);
        }
        
        [Benchmark]
        public void HashRing2()
        {
            var id = Ids[_i++ % Ids.Length];
            var owner = _hashRing2.GetNode(id);
        }
    }
}