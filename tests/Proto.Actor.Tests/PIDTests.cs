using System;
using Proto.Tests.Fixture;
using Xunit;

namespace Proto.Tests
{
    public class PIDTests
    {
        [Fact]
        public void Given_ActorNotDead_Ref_ShouldReturnIt()
        {
            var pid = Actor.Spawn(Actor.FromFunc(ActorFixture.EmptyReceive));

            var p = pid.Ref;

            Assert.NotNull(p);
        }

        [Fact]
        public void Given_ActorDied_Ref_ShouldNotReturnIt()
        {
            var pid = Actor.Spawn(Actor.FromFunc(ActorFixture.EmptyReceive).WithMailbox(() => new ActorFixture.TestMailbox()));
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
    }
}
