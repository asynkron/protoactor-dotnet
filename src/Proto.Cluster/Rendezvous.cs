// -----------------------------------------------------------------------
//   <copyright file="Rendezvous.cs" company="Asynkron HB">
//       Copyright (C) 2015-2018 Asynkron HB All rights reserved
//   </copyright>
// -----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace Proto.Cluster
{
    /// <summary>
    /// A dotnet port of rendezvous.go
    /// </summary>
    public class Rendezvous
    {
        private static readonly HashAlgorithm HashAlgorithm = FNV1A32.Create();

        struct MemberData
        {
            public MemberData(MemberStatus memberStatus)
            {
                Status = memberStatus;
                Hash = Encoding.UTF8.GetBytes(memberStatus.Address);
            }

            public MemberStatus Status { get; }
            public byte[] Hash { get; }
        }

        private MemberData[] _members = Array.Empty<MemberData>();

        public string GetNode(string key)
        {
            if (_members == null || _members.Length == 0) return "";

            if (_members.Length == 1) return _members[0].Status.Address;

            var keyBytes = Encoding.UTF8.GetBytes(key);

            uint maxScore = 0;
            MemberStatus? maxNode = null;

            foreach (var member in _members)
            {
                var hashBytes = member.Hash;
                var score = RdvHash(hashBytes, keyBytes);

                if (score > maxScore)
                {
                    maxScore = score;
                    maxNode = member.Status;
                }
            }

            return maxNode?.Address ?? "";
        }

        // ReSharper disable once ParameterTypeCanBeEnumerable.Global
        public void UpdateMembers(List<MemberStatus> members)
            => _members = members
                .Where(x => x.Alive)
                .Select(x => new MemberData(x))
                .ToArray();

        private static uint RdvHash(byte[] node, byte[] key)
        {
            var hashBytes = MergeBytes(key, node);
            var digest = HashAlgorithm.ComputeHash(hashBytes);
            var hash = BitConverter.ToUInt32(digest, 0);
            return hash;
        }

        private static byte[] MergeBytes(byte[] front, byte[] back)
        {
            var combined = new byte[front.Length + back.Length];
            Array.Copy(front, combined, front.Length);
            Array.Copy(back, 0, combined, front.Length, back.Length);
            return combined;
        }
    }
}
