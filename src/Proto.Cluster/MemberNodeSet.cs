using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Proto.Cluster
{
    internal struct MemberNode
    {
        internal static MemberNode Create(string name, bool alive, int weight)
        {
            return new MemberNode(name, alive, weight);
        }

        public string Name { get; }
        public byte[] NameBytes { get; }
        public bool Alive { get; }
        public int Weight { get; }

        private MemberNode(string name, bool alive, int weight)
        {
            Name = name;
            NameBytes = Encoding.UTF8.GetBytes(name);
            Alive = alive;
            Weight = weight;
        }
    }

    internal class MemberNodeSet
    {
        internal List<MemberNode> nodes;
        private Rendezvous rdv;
        private RoundRobin rr;

        internal MemberNodeSet()
        {
            nodes = new List<MemberNode>();
            rdv = new Rendezvous(this);
            rr = new RoundRobin(this);
        }

        internal void Add(string name, bool alive, int weight)
        {
            for (int i = 0; i < nodes.Count; i++)
            {
                if (nodes[i].Name == name)
                {
                    nodes[i] = MemberNode.Create(name, alive, weight);
                    rr.UpdateRR();
                    return;
                }
            }
            nodes.Add(MemberNode.Create(name, alive, weight));
            rr.UpdateRR();
        }

        internal void Remove(string name)
        {
            for (int i = 0; i < nodes.Count; i++)
            {
                if (nodes[i].Name == name)
                {
                    nodes.RemoveAt(i);
                    rr.UpdateRR();
                    return;
                }
            }
        }

        internal int Length() => nodes.Count;

        internal string GetNodeByRendezvous(string key)
        {
            return rdv.GetNode(key);
        }

        internal string GetNodeByRoundRobin()
        {
            return rr.GetNode();
        }

        internal string[] GetAllMemberAddresses(bool onlyAlive)
        {
            return nodes.FindAll(m => !onlyAlive || (onlyAlive && m.Alive)).Select(m => m.Name).ToArray();
        }
    }
}