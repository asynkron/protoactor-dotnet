using System;

namespace Proto.TestKit.Tests
{
    public class TestKit : TestKitBase, IDisposable
    {
        public TestKit() => SetUp();

        public void Dispose() => TearDown();
    }
}