
using System;
using Xunit;

namespace Proto.Cluster.Tests
{
    [Trait("Category", "Cluster")]
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
            //arrange
            var cache=new PidCache();
            var pid = new PID("member2:123", "some actor");
            cache.TryAddCache("someIdentity", pid);
            
            var pid2 = new PID("member1:12", "some actor2");
            cache.TryAddCache("someOtherIdentity", pid2);
            
            //act
            cache.RemoveByMemberAddress("member2:123");
            
            //assert
            //pids from member2 should be removed
            var result = cache.TryGetCache("someIdentity", out _);
            Assert.False(result);
            
            //pids from member1 should still be here
            var result2 = cache.TryGetCache("someOtherIdentity", out _);
            Assert.True(result2);
        }
    }
}
