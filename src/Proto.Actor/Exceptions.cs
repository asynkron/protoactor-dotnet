using System;

namespace Proto
{
    public class ProcessNameExistException : Exception
    {
        public string Name { get; }
        public PID Pid { get; }

        public ProcessNameExistException(string name, PID pid) : base($"a Process with the name '{name}' already exists")
        {
            Name = name;
            Pid = pid;
        }
    }
}