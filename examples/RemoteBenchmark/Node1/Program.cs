using System;
using Proto;
using Proto.Remote;
using ProtosReflection = Messages.ProtosReflection;

class Program
{
    static void Main(string[] args)
    {
        Serialization.RegisterFileDescriptor(ProtosReflection.Descriptor);
        RemotingSystem.Start("127.0.0.1", 8081);

        var remote = new PID("127.0.0.1:8080", "remote");
        remote.Tell(new Messages.Start());

        Console.ReadLine();
    }
}