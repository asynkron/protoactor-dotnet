using System.Threading;

namespace Proto.Cluster
{
    public class RoundRobin
    {
        private int val;

        private IMemberStrategy m;

        public RoundRobin(IMemberStrategy m)
        {
            this.m = m;
        }

        public string GetNode()
        {
            var members = m.GetAllMembers();
            var l = members.Count;
            if (l == 0) return "";
            if (l == 1) return members[0].Address;

            Interlocked.Increment(ref val);

            return members[val % l].Address;
        }
    }
}