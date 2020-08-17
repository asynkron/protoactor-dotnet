using System.Threading;

namespace Proto.Cluster
{
    public class RoundRobinMemberSelector
    {
        private readonly IMemberStrategy _memberStrategy;
        private int _val;

        public RoundRobinMemberSelector(IMemberStrategy memberStrategy)
        {
            _memberStrategy = memberStrategy;
        }

        public string GetMember()
        {
            var members = _memberStrategy.GetAllMembers();
            var l = members.Count;
            switch (l)
            {
                case 0: return "";
                case 1: return members[0].Address;
                default:
                {
                    var nv = Interlocked.Increment(ref _val);
                    return members[nv % l].Address;
                }
            }
        }
    }
}