// -----------------------------------------------------------------------
// <copyright file="Program.cs" company="Asynkron AB">
//      Copyright (C) 2015-2022 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------
using System;
using System.Threading;
using System.Threading.Tasks;
using Proto;

class Program
{
    private static void Main(string[] args)
    {
        var rootContext = new RootContext(new ActorSystem());
        var c = 0;
        var props = Props.FromFunc(context => {
                switch (context.Message)
                {
                    case Started _:
                        Console.WriteLine($"{DateTime.Now} Started");
                        context.SetReceiveTimeout(TimeSpan.FromSeconds(1));
                        break;
                    case ReceiveTimeout _:
                        c++;
                        Console.WriteLine($"{DateTime.Now} ReceiveTimeout: {c}");
                        break;
                    case NoInfluence _:
                        Console.WriteLine($"{DateTime.Now} Received a no-influence message");
                        break;
                    case string s:
                        Console.WriteLine($"{DateTime.Now} Received message: {s}");
                        break;
                }

                return Task.CompletedTask;
            }
        );
        var pid = rootContext.Spawn(props);

        for (var i = 0; i < 6; i++)
        {
            rootContext.Send(pid, "hello");
            Thread.Sleep(500);
        }

        Console.WriteLine("Hit [return] to send no-influence messages");
        Console.ReadLine();

        for (var i = 0; i < 6; i++)
        {
            rootContext.Send(pid, new NoInfluence());
            Thread.Sleep(500);
        }

        Console.WriteLine("Hit [return] to send a message to cancel the timeout");
        Console.ReadLine();

        rootContext.Send(pid, "cancel");

        Console.WriteLine("Hit [return] to finish");
        Console.ReadLine();
    }
}

class NoInfluence : INotInfluenceReceiveTimeout
{
}