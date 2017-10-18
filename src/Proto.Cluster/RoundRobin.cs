using System.Threading;

namespace Proto.Cluster
{
    public class RoundRobin
    {
        private int _val;

        private IMemberStrategy _m;

        public RoundRobin(IMemberStrategy m)
        {
            this._m = m;
        }

        public string GetNode()
        {
            var members = _m.GetAllMembers();
            var l = members.Count;
            if (l == 0) return "";
            if (l == 1) return members[0].Address;

            Interlocked.Increment(ref _val);

            return members[_val % l].Address;
        }
    }
}