// -----------------------------------------------------------------------
//   <copyright file="Program.cs" company="Asynkron HB">
//       Copyright (C) 2015-2017 Asynkron HB All rights reserved
//   </copyright>
// -----------------------------------------------------------------------

using System;
using System.Threading.Tasks;
using Proto;

class Program
{
    static void Main(string[] args)
    {
        var actor = Actor.FromFunc(c =>
                         {
                             if (c.MessageHeader.ContainsKey("TraceID"))
                             {
                                 Console.WriteLine($"TraceID = {c.MessageHeader.GetOrDefault("TraceID")}");
                                 Console.WriteLine($"SpanID = {c.MessageHeader.GetOrDefault("SpanID")}");
                                 Console.WriteLine($"ParentSpanID = {c.MessageHeader.GetOrDefault("ParentSpanID")}");
                             }
                             Console.WriteLine($"actor got {c.Message.GetType()}:{c.Message}");
                             return Actor.Done;
                         })
                         .WithReceiveMiddleware(
                             next => async c =>
                             {
                                 Console.WriteLine($"middleware 1 enter {c.Message.GetType()}:{c.Message}");
                                 await next(c);
                                 Console.WriteLine($"middleware 1 exit {c.Message.GetType()}:{c.Message}");
                             },
                             next => async c =>
                             {
                                 Console.WriteLine($"middleware 2 enter {c.Message.GetType()}:{c.Message}");
                                 await next(c);
                                 Console.WriteLine($"middleware 2 exit {c.Message.GetType()}:{c.Message}");
                             });

        var pid = Actor.Spawn(actor);

        //Set headers, e.g. Zipkin trace headers
        var rootHeaders = new MessageHeader
        {
            {"TraceID", "1000"},
            {"SpanID", "2000"}
        };

        var root = new RootContext(rootHeaders, next => async (c, target, envelope) =>
                                   {
                                       envelope.SetHeader("TraceID", c.MessageHeader.GetOrDefault("TraceID"));
                                       envelope.SetHeader("SpanID", c.MessageHeader.GetOrDefault("SpanID"));
                                       envelope.SetHeader("ParentSpanID", c.MessageHeader.GetOrDefault("ParentSpanID"));

                                       Console.WriteLine($"sender middleware 1 enter {envelope.Message.GetType()}:{c.Message}");
                                       await next(c, target, envelope);
                                       Console.WriteLine($"sender middleware 1 exit {envelope.Message.GetType()}:{c.Message}");
                                   },
                                   next => async (c, target, envelope) =>
                                   {
                                       Console.WriteLine($"sender middleware 2 enter {envelope.Message.GetType()}:{c.Message}");
                                       await next(c, target, envelope);
                                       Console.WriteLine($"sender middleware 2 exit {envelope.Message.GetType()}:{c.Message}");
                                   });
        //just wait for started message to be processed to make the output look less confusing
        Task.Delay(500).Wait();
        root.Tell(pid, "hello");

        Console.ReadLine();
        Console.ReadLine();
    }
}