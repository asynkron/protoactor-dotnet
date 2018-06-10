using System;
using Proto.TestFixtures;
using Xunit;

namespace Proto.Tests
{
    public class ProcessRegistryTests
    {
        [Fact]
        public void Given_PIDDoesNotExist_TryAddShouldAddLocalPID()
        {
            var id = Guid.NewGuid().ToString();
            var p = new TestProcess();
            var reg = new ProcessRegistry();

            var (pid, ok) = reg.TryAdd(id, p);

            Assert.True(ok);
            Assert.Equal(reg.Address, pid.Address);
        }

        [Fact]
        public void Given_PIDExists_TryAddShouldNotAddLocalPID()
        {
            var id = Guid.NewGuid().ToString();
            var p = new TestProcess();
            var reg = new ProcessRegistry();
            reg.TryAdd(id, p);

            var (_, ok) = reg.TryAdd(id, p);

            Assert.False(ok);
        }

        [Fact]
        public void Given_PIDExists_GetShouldReturnIt()
        {
            var id = Guid.NewGuid().ToString();
            var p = new TestProcess();
            var reg = new ProcessRegistry();
            reg.TryAdd(id, p);
            var (pid, _) = reg.TryAdd(id, p);

            var p2 = reg.Get(pid);

            Assert.Same(p, p2);
        }

        [Fact]
        public void Given_PIDWasRemoved_GetShouldReturnDeadLetterProcess()
        {
            var id = Guid.NewGuid().ToString();
            var p = new TestProcess();
            var reg = new ProcessRegistry();
            var (pid, _) = reg.TryAdd(id, p);
            reg.Remove(pid);

            var p2 = reg.Get(pid);

            Assert.Same(DeadLetterProcess.Instance, p2);
        }

        [Fact]
        public void Given_PIDExistsInHostResolver_GetShouldReturnIt()
        {
            var pid = new PID();
            var p = new TestProcess();
            var reg = new ProcessRegistry();
            reg.RegisterHostResolver(x => p);

            var p2 = reg.Get(pid);

            Assert.Same(p, p2);
        }
    }
}
