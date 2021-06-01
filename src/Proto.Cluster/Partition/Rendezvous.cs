// -----------------------------------------------------------------------
// <copyright file="Rendezvous.cs" company="Asynkron AB">
//      Copyright (C) 2015-2020 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Proto.Cluster.Partition
{
    /// <summary>
    ///     A dotnet port of rendezvous.go
    /// </summary>
    public class Rendezvous
    {
        private MemberData[] _members = Array.Empty<MemberData>();

        public string GetOwnerMemberByIdentity(string identity)
        {
            switch (_members.Length)
            {
                case 0:
                    return "";
                case 1:
                    return _members[0].Info.Address;
            }

            var keyBytes = Encoding.UTF8.GetBytes(identity);

            uint maxScore = 0;
            Member? maxNode = null;

            foreach (var member in _members)
            {
                var hashBytes = member.Hash;
                var score = RdvHash(hashBytes, keyBytes);

                if (score <= maxScore) continue;

                maxScore = score;
                maxNode = member.Info;
            }

            return maxNode?.Address ?? "";
        }

        // ReSharper disable once ParameterTypeCanBeEnumerable.Global
        public void UpdateMembers(IEnumerable<Member> members) => _members = members
            .OrderBy(m => m.Address)
            .Select(x => new MemberData(x))
            .ToArray();

        private static uint RdvHash(byte[] node, byte[] key)
        {
            var hashBytes = MergeBytes(key, node);
            return MurmurHash2.Hash(hashBytes);
        }

        private static byte[] MergeBytes(byte[] front, byte[] back)
        {
            var combined = new byte[front.Length + back.Length];
            Array.Copy(front, combined, front.Length);
            Array.Copy(back, 0, combined, front.Length, back.Length);
            return combined;
        }

        private readonly struct MemberData
        {
            public MemberData(Member member)
            {
                Info = member;
                Hash = Encoding.UTF8.GetBytes(member.Address);
            }

            public Member Info { get; }
            public byte[] Hash { get; }
        }

        public void Debug()
        {
            foreach (var m in _members)
            {
                Console.WriteLine(m.Info);
            }
        }
    }
}