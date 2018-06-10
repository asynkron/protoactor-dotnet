// -----------------------------------------------------------------------
//   <copyright file="Program.cs" company="Asynkron HB">
//       Copyright (C) 2015-2018 Asynkron HB All rights reserved
//   </copyright>
// -----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Proto;

class Program
{
    static void Main(string[] args)
    {
        //Set headers, e.g. Zipkin trace headers
        var headers = new MessageHeader(
            new Dictionary<string, string>
            {
                {"TraceID", Guid.NewGuid().ToString()},
                {"SpanID", Guid.NewGuid().ToString()}
            }
        );

        var root = new RootContext(
            headers,
            next => async (c, target, envelope) =>
            {
                var newEnvelope = envelope
                    .WithHeader("TraceID", c.Headers.GetOrDefault("TraceID"))
                    .WithHeader("SpanID", Guid.NewGuid().ToString())
                    .WithHeader("ParentSpanID", c.Headers.GetOrDefault("SpanID"));

                Console.WriteLine(" 1 Enter RootContext SenderMiddleware");
                Console.WriteLine(" 1 TraceID: " + newEnvelope.Header.GetOrDefault("TraceID"));
                Console.WriteLine(" 1 SpanID: " + newEnvelope.Header.GetOrDefault("SpanID"));
                Console.WriteLine(" 1 ParentSpanID: " + newEnvelope.Header.GetOrDefault("ParentSpanID"));
                await next(c, target, newEnvelope);
                //this line might look confusing at first when reading the console output
                //it looks like this finishes before the actor receive middleware kicks in
                //which is exactly what it does, due to the actor mailbox.
                //that is, the sender side of things just put the message on the mailbox and exits
                Console.WriteLine(" 1 Exit RootContext SenderMiddleware - Send is async, this is out of order by design");
            });
        
        var actor = Props.FromFunc(
                c =>
                {
                    if (c.Message is string)
                    {
                        Console.WriteLine("   3 Enter Actor");
                        Console.WriteLine($"   3 TraceID = {c.Headers.GetOrDefault("TraceID")}");
                        Console.WriteLine($"   3 SpanID = {c.Headers.GetOrDefault("SpanID")}");
                        Console.WriteLine($"   3 ParentSpanID = {c.Headers.GetOrDefault("ParentSpanID")}");
                        Console.WriteLine($"   3 actor got {c.Message.GetType()}:{c.Message}");
                        c.Respond("World !");
                        Console.WriteLine("   3 Exit Actor");
                    }

                    return Actor.Done;
                })
            .WithReceiveMiddleware(next => async (context, envelope) =>
            {
                if (envelope.Message is string)
                {
                    var newEnvelope = envelope
                        .WithHeader("TraceID", envelope.Header.GetOrDefault("TraceID"))
                        .WithHeader("SpanID", Guid.NewGuid().ToString())
                        .WithHeader("ParentSpanID", envelope.Header.GetOrDefault("SpanID"));

                    Console.WriteLine("  2 Enter Actor ReceiverMiddleware");
                    Console.WriteLine("  2 TraceID: " + newEnvelope.Header.GetOrDefault("TraceID"));
                    Console.WriteLine("  2 SpanID: " + newEnvelope.Header.GetOrDefault("SpanID"));
                    Console.WriteLine("  2 ParentSpanID: " + newEnvelope.Header.GetOrDefault("ParentSpanID"));
                    await next(context, newEnvelope);
                    Console.WriteLine("  2 Exit Actor ReceiverMiddleware");
                }
                else
                {  
                    await next(context, envelope);   
                }
            }).WithSenderMiddleware(next => async (context, target, envelope) =>
            {
                var newEnvelope = envelope
                    .WithHeader("TraceID", context.Headers.GetOrDefault("TraceID"))
                    .WithHeader("SpanID", Guid.NewGuid().ToString())
                    .WithHeader("ParentSpanID", context.Headers.GetOrDefault("SpanID"));

                Console.WriteLine("    4 Enter Actor SenderMiddleware");
                Console.WriteLine("    4 TraceID: " + newEnvelope.Header.GetOrDefault("TraceID"));
                Console.WriteLine("    4 SpanID: " + newEnvelope.Header.GetOrDefault("SpanID"));
                Console.WriteLine("    4 ParentSpanID: " + newEnvelope.Header.GetOrDefault("ParentSpanID"));
                await next(context, target, envelope);
                Console.WriteLine("    4 Exit Actor SenderMiddleware");
            });
        var pid = root.Spawn(actor);
        
        Task.Delay(500).Wait();
        Console.WriteLine("0 TraceID: " + root.Headers.GetOrDefault("TraceID"));
        Console.WriteLine("0 SpanID: " + root.Headers.GetOrDefault("SpanID"));
        Console.WriteLine("0 ParentSpanID: " + root.Headers.GetOrDefault("ParentSpanID"));

        var res = root.RequestAsync<string>(pid, "hello").Result;
        Console.WriteLine("Got result " + res);

        Console.ReadLine();
    }
}