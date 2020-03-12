using System;
using Proto.TestFixtures;
using Xunit;

namespace Proto.Tests
{
    public class ProcessRegistryTests
    {
        private static readonly ActorSystem System = new ActorSystem();
        [Fact]
        public void Given_PIDDoesNotExist_TryAddShouldAddLocalPID()
        {
            var id = Guid.NewGuid().ToString();
            var p = new TestProcess(System);
            var reg = new ProcessRegistry(System);

            var (pid, ok) = reg.TryAdd(id, p);

            Assert.True(ok);
            Assert.Equal(reg.Address, pid.Address);
        }

        [Fact]
        public void Given_PIDExists_TryAddShouldNotAddLocalPID()
        {
            var id = Guid.NewGuid().ToString();
            var p = new TestProcess(System);
            var reg = new ProcessRegistry(System);
            reg.TryAdd(id, p);

            var (_, ok) = reg.TryAdd(id, p);

            Assert.False(ok);
        }

        [Fact]
        public void Given_PIDExists_GetShouldReturnIt()
        {
            var id = Guid.NewGuid().ToString();
            var p = new TestProcess(System);
            var reg = new ProcessRegistry(System);
            reg.TryAdd(id, p);
            var (pid, _) = reg.TryAdd(id, p);

            var p2 = reg.Get(pid);

            Assert.Same(p, p2);
        }

        [Fact]
        public void Given_PIDWasRemoved_GetShouldReturnDeadLetterProcess()
        {
            var id = Guid.NewGuid().ToString();
            var p = new TestProcess(System);
            var reg = new ProcessRegistry(System);
            var (pid, _) = reg.TryAdd(id, p);
            reg.Remove(pid);

            var p2 = reg.Get(pid);

            Assert.Same(System.DeadLetter, p2);
        }

        [Fact]
        public void Given_PIDExistsInHostResolver_GetShouldReturnIt()
        {
            var pid = new PID();
            var p = new TestProcess(System);
            var reg = new ProcessRegistry(System);
            reg.RegisterHostResolver(x => p);

            var p2 = reg.Get(pid);

            Assert.Same(p, p2);
        }
    }
}
