using System;
using Proto.TestFixtures;
using Xunit;

using static Proto.TestFixtures.Receivers;

namespace Proto.Tests
{
    public class PIDTests
    {
        private static readonly RootContext Context = new RootContext();
        [Fact]
        public void Given_ActorNotDead_Ref_ShouldReturnIt()
        {
            var pid = Context.Spawn(Props.FromFunc(EmptyReceive));

            var p = pid.Ref;

            Assert.NotNull(p);
        }

        [Fact]
        public async void Given_ActorDied_Ref_ShouldNotReturnIt()
        {
            var pid = Context.Spawn(Props.FromFunc(EmptyReceive).WithMailbox(() => new TestMailbox()));
            await pid.StopAsync();

            var p = pid.Ref;

            Assert.Null(p);
        }

        [Fact]
        public void Given_OtherProcess_Ref_ShouldReturnIt()
        {
            var id = Guid.NewGuid().ToString();
            var p = new TestProcess();
            var (pid, _) = ProcessRegistry.Instance.TryAdd(id, p);

            var p2 = pid.Ref;

            Assert.Same(p, p2);
        }
    }
}
