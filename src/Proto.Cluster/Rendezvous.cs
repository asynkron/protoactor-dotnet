using System;
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

        private readonly IMemberStrategy _strategy;
        private byte[][] _memberHashes;

        public Rendezvous(IMemberStrategy strategy)
        {
            _strategy = strategy;
            UpdateRdv();
        }

        public string GetNode(string key)
        {
            var members = _strategy.GetAllMembers();

            if (members == null || members.Count == 0)
                return "";

            if (members.Count == 1)
                return members[0].Address;

            var keyBytes = Encoding.UTF8.GetBytes(key);

            uint maxScore = 0;
            MemberStatus maxNode = null;

            for(int i = 0; i < members.Count; i++)
            {
                var member = members[i];
                if (member.Alive)
                {
                    var hashBytes = _memberHashes[i];
                    var score = RdvHash(hashBytes, keyBytes);
                    if (score > maxScore)
                    {
                        maxScore = score;
                        maxNode = member;
                    }
                }
            }

            return maxNode == null ? "" : maxNode.Address;
        }

        public void UpdateRdv()
        {
            _memberHashes = _strategy.GetAllMembers().Select(mb => Encoding.UTF8.GetBytes(mb.Address)).ToArray();
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