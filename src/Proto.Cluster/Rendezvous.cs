using System;
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
            public MemberStatus Status { get; set; }
            public byte[] Hash { get; set; }
        }

        private List<MemberData> _members = new List<MemberData>();

        public string GetNode(string key)
        {
            if (_members == null || _members.Count == 0)
                return "";

            if (_members.Count == 1)
                return _members[0].Status.Address;

            var keyBytes = Encoding.UTF8.GetBytes(key);

            uint maxScore = 0;
            MemberStatus maxNode = null;
            uint score;

            for (int i = 0; i < _members.Count; i++)
            {
                var member = _members[i];
                var hashBytes = member.Hash;
                score = RdvHash(hashBytes, keyBytes);
                if (score > maxScore)
                {
                    maxScore = score;
                    maxNode = member.Status;
                }
            }

            return maxNode == null ? "" : maxNode.Address;
        }

        public void UpdateMembers(IEnumerable<MemberStatus> members)
        {
            // Store the members list to be used by GetNode().
            _members.Clear();
            foreach (var memberStatus in members)
            {
                // Skip members that are not alive.
                if (memberStatus.Alive == false)
                    continue;

                // Calculate hash based on member's address.
                byte[] hash = Encoding.UTF8.GetBytes(memberStatus.Address);
                _members.Add(new MemberData()
                {
                    Status = memberStatus,
                    Hash = hash,
                });
            }
        }

        private static uint RdvHash(byte[] node, byte[] key)
        {
            var hashBytes = MergeBytes(key, node);
            var digest = HashAlgorithm.ComputeHash(hashBytes);
            var hash = BitConverter.ToUInt32(digest, 0);
            return hash;
        }

        private static byte[] MergeBytes(byte[] front, byte[] back)
        {
            byte[] combined = new byte[front.Length + back.Length];
            Array.Copy(front, combined, front.Length);
            Array.Copy(back, 0, combined, front.Length, back.Length);
            return combined;
        }
    }
}