using System.Collections.Generic;
using System.Linq;

namespace Proto.Cluster
{
    public static class MemberExtensions
    {
        public static string ToLogString(this IEnumerable<MemberInfo> self)
        {
            var members = "[" + string.Join(", ", self.Select(m => m.Address)) + "]";
            return members;
        }
    }
}