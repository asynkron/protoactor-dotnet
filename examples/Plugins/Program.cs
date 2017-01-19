using System;
using System.Collections.Generic;
using Proto;

namespace PluginsExample
{
    class Program
    {
        static void Main(string[] args)
        {
            var actor = Actor.FromFunc(c =>
                {
                    Console.WriteLine($"actor got {c.Message.GetType()}:{c.Message}");
                    return Actor.Done;
                })
                .WithReceivers(
                    async c =>
                    {
                        Console.WriteLine($"receiver 1 got {c.Message.GetType()}:{c.Message}");
                        await c.NextAsync();
                        Console.WriteLine($"receiver 1 got control back");
                    },
                    async c =>
                    {
                        Console.WriteLine($"receiver 2 got {c.Message.GetType()}:{c.Message}");
                        await c.NextAsync();
                        Console.WriteLine($"receiver 2 got control back");
                    });

            var pid = Actor.Spawn(actor);
            pid.Tell("hello");
            Console.ReadLine();
            Console.ReadLine();
        }
    }
}