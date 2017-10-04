using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;

namespace Proto.Cluster
{
    /// <summary>
    /// A dotnet port of rendezvous.go
    /// </summary>
    internal class Rendezvous
    {
        private static readonly HashAlgorithm HashAlgorithm = FNV1A32.Create();

        private MemberNodeSet m;

        internal Rendezvous(MemberNodeSet m)
        {
            this.m = m;
        }
        
        internal string GetNode(string key)
        {
            if (m.nodes == null || m.nodes.Count == 0)
                return "";

            if (m.nodes.Count == 1)
                return m.nodes[0].Name;

            var keyBytes = Encoding.UTF8.GetBytes(key);

            uint maxScore = 0;
            string maxNode = "";
            uint score = 0;

            foreach (var node in m.nodes)
            {
                if (node.Alive)
                {
                    score = RdvHash(node.NameBytes, keyBytes);
                    if (score > maxScore)
                    {
                        maxScore = score;
                        maxNode = node.Name;
                    }
                }
            }

            return maxNode;
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