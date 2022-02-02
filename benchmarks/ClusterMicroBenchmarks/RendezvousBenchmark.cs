// -----------------------------------------------------------------------
// <copyright file="RendezvousBenchmark.cs" company="Asynkron AB">
//      Copyright (C) 2015-2022 Asynkron AB All rights reserved
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
        [Params(1, 10, 100)]
        public int NodeCount { get; set; }

        private int _i = 0;
        private static readonly string[] Ids = Enumerable.Range(0, 100).Select(_ => Guid.NewGuid().ToString()).ToArray();

        private Rendezvous _rendezvous;
        private MemberHashRing _memberHashRing;
        private HashRing<Member> _hashRing;

        [GlobalSetup]
        public void Setup()
        {
            var members = Enumerable.Range(0, NodeCount).Select(i => new Member
                {
                    Host = "localhost",
                    Id = Guid.NewGuid().ToString("N"),
                    Port = i + 1000
                }
            ).ToArray();
            _rendezvous = new Rendezvous();
            _rendezvous.UpdateMembers(members);
            _memberHashRing = new MemberHashRing(members);
            _hashRing = new HashRing<Member>(members, member => member.Address, MurmurHash2.Hash, 50);
        }

        [Benchmark]
        public void Rendezvous()
        {
            var owner = _rendezvous.GetOwnerMemberByIdentity(TestId());
        }

        [Benchmark]
        public void MemberRing()
        {
            var owner = _memberHashRing.GetOwnerMemberByIdentity(TestId());
        }

        [Benchmark]
        public void HashRing()
        {
            var owner = _hashRing.GetNode(TestId());
        }

        private string TestId() => Ids[_i++ % Ids.Length];
    }
}