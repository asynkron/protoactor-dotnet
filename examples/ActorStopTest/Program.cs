// -----------------------------------------------------------------------
//  <copyright file="Program.cs" company="Asynkron HB">
//      Copyright (C) 2015-2017 Asynkron HB All rights reserved
//  </copyright>
// -----------------------------------------------------------------------

using System;
using System.Threading.Tasks;
using Proto;

public class Program
{
    static void Main(string[] args)
    {
        var system = new ActorSystem();
        var props = Props.FromProducer(() => new ProcessActor());
        var pid = system.Root.Spawn(props);
        system.Root.Send(pid, new Process());
        Task.Run(async () =>
        {
            await Task.Delay(50);
            system.Root.Stop(pid);
        });
        Console.ReadLine();
    }

    internal class Process { }

    internal class ProcessActor : IActor
    {
        public Task ReceiveAsync(IContext context)
        {
            switch (context.Message)
            {
                case Started _:
                    Console.WriteLine("Started");
                    break;

                case Process _:
                    Console.WriteLine("Process");
                    context.Send(context.Self, new Process());
                    break;

                case Stopping _:
                    Console.WriteLine("Stopping");
                    break;

                case Stopped _:
                    Console.WriteLine("Stopped");
                    break;
            }
            return Actor.Done;
        }
    }
}