using System;
using Google.Protobuf.WellKnownTypes;
using Proto.TestFixtures;
using Xunit;

using static Proto.TestFixtures.Receivers;

namespace Proto.Tests
{
    public class PIDTests
    {
        [Fact]
        public void Given_ActorNotDead_Ref_ShouldReturnIt()
        {
            var pid = Actor.Spawn(Actor.FromFunc(EmptyReceive));

            var p = pid.Ref;

            Assert.NotNull(p);
        }

        [Fact]
        public void Given_ActorDied_Ref_ShouldNotReturnIt()
        {
            var pid = Actor.Spawn(Actor.FromFunc(EmptyReceive).WithMailbox(() => new TestMailbox()));
            pid.Stop();

            var p = pid.Ref;

            Assert.Null(p);
        }

        [Fact]
        public void Given_OtherProcess_Ref_ShouldReturnIt()
        {
            var id = Guid.NewGuid().ToString();
            var p = new TestProcess();
            var (pid, ok) = ProcessRegistry.Instance.TryAdd(id, p);

            var p2 = pid.Ref;

            Assert.Same(p, p2);
        }


        [Fact]
        public void Given_TwoProcessHosts_CanUseSameId()
        {
            var host = new ProcessHost("testHost");
            ProcessRegistry.Instance.RegisterProcessHost(host);

            var props = Actor.FromFunc(_ => Actor.Done);
            var id = Guid.NewGuid().ToString();

            var pid1 = Actor.SpawnNamed(props, id);
            var pid2 = Actor.SpawnNamed(props.WithProcessHost(host), id);

            Assert.Equal(pid1.Id, pid2.Id);
            Assert.NotEqual(pid1.Address, pid2.Address);
            Assert.Equal(pid1.Address, ProcessRegistry.Instance.Address);
            Assert.Equal(pid2.Address, host.Address);
        }

        [Fact]
        public void Given_TwoProcessHosts_ResolvesCorrectly()
        {
            var host = new ProcessHost("testHost2");
            ProcessRegistry.Instance.RegisterProcessHost(host);

            var props1 = Actor.FromFunc(ctx =>
            {
                if (ctx.Message is Empty) ctx.Respond("pid1");
                return Actor.Done;
            });

            var props2 = Actor.FromFunc(ctx =>
            {
                if (ctx.Message is Empty) ctx.Respond("pid2");
                return Actor.Done;
            });

            var id = Guid.NewGuid().ToString();
            var pid1 = Actor.SpawnNamed(props1, id);
            var pid2 = Actor.SpawnNamed(props2.WithProcessHost(host), id);

            var s1 = pid1.RequestAsync<string>(new Empty()).Result;
            var s2 = pid2.RequestAsync<string>(new Empty()).Result;

            Assert.Equal(s1, "pid1");
            Assert.Equal(s2, "pid2");
        }
    }
}
