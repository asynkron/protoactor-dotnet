using System;

namespace FSMExample
{
    public class IncorrectStateException : Exception
    {
        public IncorrectStateException(string msg): base(msg)
        {
        }
    }
}