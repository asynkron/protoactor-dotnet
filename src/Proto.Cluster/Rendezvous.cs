using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;

namespace Proto.Cluster
{
    internal class MemberNode
    {
        internal static MemberNode Create(string name, bool alive = false)
        {
            return new MemberNode(name, alive);
        }
        
        public string Name { get; }
        public byte[] NameBytes { get; }
        public bool Alive { get; }

        private MemberNode(string name, bool alive)
        {
            Name = name;
            NameBytes = Encoding.UTF8.GetBytes(name);
            Alive = alive;
        }

        public override bool Equals(object obj)
        {
            if (!(obj is MemberNode))
                return false;
            var other = (MemberNode) obj;
            return GetHashCode() == other.GetHashCode();
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hash = 17;
                for (var i = 0; i < NameBytes.Length; i++)
                {
                    hash = hash * 23 + NameBytes[i];
                }
                return hash;
            }
        }
    }

    /// <summary>
    /// A dotnet port of rendezvous.go
    /// </summary>
    internal static class Rendezvous
    {
        private static readonly HashAlgorithm HashAlgorithm = FNV1A32.Create();

        internal static string GetNode(HashSet<MemberNode> nodes, string key)
        {
            if (nodes == null || nodes.Count == 0)
                return "";

            var keyBytes = Encoding.UTF8.GetBytes(key);

            uint maxScore = 0;
            MemberNode maxNode = null;
            uint score = 0;

            foreach (var node in nodes)
            {
                if (node.Alive)
                {
                    score = RdvHash(node.NameBytes, keyBytes);
                    if (score > maxScore)
                    {
                        maxScore = score;
                        maxNode = node;
                    }
                }
            }

            return maxNode == null ? "" : maxNode.Name;
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