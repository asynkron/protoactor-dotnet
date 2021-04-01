using System;
using Proto.TestFixtures;
using Xunit;

namespace Proto.Tests
{
    public class ProcessRegistryTests
    {
        private static readonly ActorSystem System = new();

        [Fact]
        public void Given_PIDDoesNotExist_TryAddShouldAddLocalPID()
        {
            string id = Guid.NewGuid().ToString();
            TestProcess p = new TestProcess(System);
            ProcessRegistry reg = new ProcessRegistry(System);

            (PID pid, bool ok) = reg.TryAdd(id, p);

            Assert.True(ok);
            Assert.Equal(System.Address, pid.Address);
        }

        [Fact]
        public void Given_PIDExists_TryAddShouldNotAddLocalPID()
        {
            string id = Guid.NewGuid().ToString();
            TestProcess p = new TestProcess(System);
            ProcessRegistry reg = new ProcessRegistry(System);
            reg.TryAdd(id, p);

            (_, bool ok) = reg.TryAdd(id, p);

            Assert.False(ok);
        }

        [Fact]
        public void Given_PIDExists_GetShouldReturnIt()
        {
            string id = Guid.NewGuid().ToString();
            TestProcess p = new TestProcess(System);
            ProcessRegistry reg = new ProcessRegistry(System);
            reg.TryAdd(id, p);
            (PID pid, _) = reg.TryAdd(id, p);

            Process p2 = reg.Get(pid);

            Assert.Same(p, p2);
        }

        [Fact]
        public void Given_PIDWasRemoved_GetShouldReturnDeadLetterProcess()
        {
            string id = Guid.NewGuid().ToString();
            TestProcess p = new TestProcess(System);
            ProcessRegistry reg = new ProcessRegistry(System);
            (PID pid, _) = reg.TryAdd(id, p);
            reg.Remove(pid);

            Process p2 = reg.Get(pid);

            Assert.Same(System.DeadLetter, p2);
        }

        [Fact]
        public void Given_PIDExistsInHostResolver_GetShouldReturnIt()
        {
            PID pid = new PID();
            TestProcess p = new TestProcess(System);
            ProcessRegistry reg = new ProcessRegistry(System);
            reg.RegisterHostResolver(x => p);

            Process p2 = reg.Get(pid);

            Assert.Same(p, p2);
        }

        [Fact]
        public void Given_PIDExistsInClientResolver_GetShouldReturnIt()
        {
            PID pid = new PID();
            pid.Address = System.Address;
            TestProcess p = new TestProcess(System);
            ProcessRegistry reg = new ProcessRegistry(System);
            reg.RegisterClientResolver(x => p);

            Process p2 = reg.Get(pid);

            Assert.Same(p, p2);
        }
    }
}
