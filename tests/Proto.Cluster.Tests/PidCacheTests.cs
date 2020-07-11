
using System;
using Xunit;

namespace Proto.Cluster.Tests
{
    [Trait("Category", "Remote")]
    public class PidCacheTests
    {

        [Fact]
        public void CanAddPid()
        {
            var cache=new PidCache();
            var pid = new PID("member2:123", "some actor");
            var result =cache.TryAddCache("someIdentity", pid);
            Assert.True(result);
        }
        
        [Fact]
        public void CanGetPid()
        {
            var cache=new PidCache();
            var pid = new PID("member2:123", "some actor");
            cache.TryAddCache("someIdentity", pid);
            var result = cache.TryGetCache("someIdentity", out var pidResult);
            Assert.True(result);
            Assert.Same(pid,pidResult);
        }
        
        [Fact]
        public void CanRemovePid()
        {
            var cache=new PidCache();
            var pid = new PID("member2:123", "some actor");
            cache.TryAddCache("someIdentity", pid);
            cache.RemoveByPid(pid);
            var result = cache.TryGetCache("someIdentity", out _);
            Assert.False(result);
        }

        
        [Fact]
        public void MemberLeft()
        {
            var cache=new PidCache();
            var pid = new PID("member2:123", "some actor");
            cache.TryAddCache("someIdentity", pid);
            
            var left = new MemberLeftEvent("member2", 123, Array.Empty<string>());
            cache.OnMemberStatusEvent(left);
        }
    }
}
