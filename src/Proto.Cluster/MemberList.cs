using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace Proto.Cluster
{
    public static class MemberList
    {
        public static async Task<IReadOnlyCollection<string>> GetMembersAsync(string kind)
        {
            var res = await MemberListActor.MemberListPID.RequestAsync<MemberByKindResponse>(new MemberByKindRequest(kind,true));
            return res.Kinds;
        }
    }

    public class MemberByKindResponse
    {
        public MemberByKindResponse(IReadOnlyCollection<string> kinds)
        {
            Kinds = kinds ?? throw new ArgumentNullException(nameof(kinds));
        }

        public IReadOnlyCollection<string> Kinds { get; set; }
    }

    public class MemberByKindRequest
    {
        public MemberByKindRequest(string kind, bool onlyAlive)
        {
            Kind = kind ??
            throw new ArgumentNullException(nameof(kind));
            OnlyAlive = onlyAlive;
        }

        public string Kind { get; }
        public bool OnlyAlive { get; }
    }
}
