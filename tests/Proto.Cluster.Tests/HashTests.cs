using Proto.Cluster.Partition;
using Xunit;

namespace Proto.Cluster.Tests
{
    public class HashTests
    {
        [Fact]
        public void EnsureHashingIsConsistent()
        {
            var rsv = new Rendezvous();
            var members = new[]
            {
                new Member
                {
                    Port = 8090,
                    Host = "127.0.0.1"
                },
                new Member
                {
                    Port = 8091,
                    Host = "127.0.0.1"
                },
                new Member
                {
                    Port = 8092,
                    Host = "127.0.0.1"
                },
                new Member
                {
                    Port = 8093,
                    Host = "127.0.0.1"
                },
                new Member
                {
                    Port = 8094,
                    Host = "127.0.0.1"
                },
                new Member
                {
                    Port = 8095,
                    Host = "127.0.0.1"
                },
                new Member
                {
                    Port = 8096,
                    Host = "127.0.0.1"
                },
                new Member
                {
                    Port = 8097,
                    Host = "127.0.0.1"
                },
                new Member
                {
                    Port = 8098,
                    Host = "127.0.0.1"
                },
                new Member
                {
                    Port = 8099,
                    Host = "127.0.0.1"
                },
                new Member
                {
                    Port = 8100,
                    Host = "127.0.0.1"
                },
            };
            
            rsv.UpdateMembers(members);

            for (int i = 0; i < 100; i++)
            {
                var res = rsv.GetOwnerMemberByIdentity("myactor4");
            
                Assert.Equal("127.0.0.1:8100",res);
            }
 
        }
        
        [Fact]
        public void EnsureHashingIsConsistent2()
        {
            var rsv = new Rendezvous();
            var members = new[]
            {
                new Member
                {
                    Port = 8094,
                    Host = "127.0.0.1"
                },
                new Member
                {
                    Port = 8090,
                    Host = "127.0.0.1"
                },
                new Member
                {
                    Port = 8091,
                    Host = "127.0.0.1"
                },
                new Member
                {
                    Port = 8093,
                    Host = "127.0.0.1"
                },
                
                new Member
                {
                    Port = 8092,
                    Host = "127.0.0.1"
                },
                new Member
                {
                    Port = 8095,
                    Host = "127.0.0.1"
                },
            };
            
            rsv.UpdateMembers(members);

            for (int i = 0; i < 100; i++)
            {
                var res = rsv.GetOwnerMemberByIdentity("myactor4");

                Assert.Equal("127.0.0.1:8095", res);
            }
        }
    }
}