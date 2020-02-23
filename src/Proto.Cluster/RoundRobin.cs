using System.Threading;

namespace Proto.Cluster
{
    public class RoundRobin
    {
        private int _val;

        private readonly IMemberStrategy _memberStrategy;

        public RoundRobin(IMemberStrategy memberStrategy)
        {
            _memberStrategy = memberStrategy;
        }

        public string GetNode()
        {
            var members = _memberStrategy.GetAllMembers();
            var l = members.Count;
            if (l == 0) return "";
            if (l == 1) return members[0].Address;

            var nv = Interlocked.Increment(ref _val);

            return members[nv % l].Address;
        }
    }
}
