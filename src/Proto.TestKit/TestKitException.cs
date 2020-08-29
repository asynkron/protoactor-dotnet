using System;

namespace Proto.TestKit
{
    public class TestKitException : Exception
    {
        public TestKitException(string message) : base(message)
        {
        }
    }
}