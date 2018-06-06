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
        var actor = Actor.FromFunc(
            c =>
            {
                if (c.Headers.ContainsKey("TraceID"))
                {
                    Console.WriteLine($"TraceID = {c.Headers.GetOrDefault("TraceID")}");
                    Console.WriteLine($"SpanID = {c.Headers.GetOrDefault("SpanID")}");
                    Console.WriteLine($"ParentSpanID = {c.Headers.GetOrDefault("ParentSpanID")}");
                }
                Console.WriteLine($"actor got {c.Message.GetType()}:{c.Message}");

                if (c.Sender != null) c.Respond("World !");

                return Actor.Done;
            });

        var pid = Actor.Spawn(actor);

        //Set headers, e.g. Zipkin trace headers
        var headers = new MessageHeader(
        new Dictionary<string, string>{
            {"TraceID", "1000"},
            {"SpanID", "2000"}
            }
        );

        var root = new RootContext(
            headers,
            next => async (c, target, envelope) =>
            {
                var newEnvelope = envelope
                    .WithHeader("TraceID", c.Headers.GetOrDefault("TraceID"))
                    .WithHeader("SpanID", c.Headers.GetOrDefault("SpanID"))
                    .WithHeader("ParentSpanID", c.Headers.GetOrDefault("ParentSpanID"))
                    .WithMessage(envelope.Message + "!");
                    
                Console.WriteLine($"sender middleware 1 enter {envelope.Message.GetType()}:{envelope.Message}");

                await next(c, target, newEnvelope);
                
                Console.WriteLine($"sender middleware 1 exit {envelope.Message.GetType()}:{envelope.Message}");
            },
            next => async (c, target, envelope) =>
            {
                Console.WriteLine($"sender middleware 2 enter {envelope.Message.GetType()}:{envelope.Message}");
                
                var newEnvelope = envelope
                    .WithMessage(envelope.Message + "?");

                await next(c, target, newEnvelope);
                Console.WriteLine($"sender middleware 2 exit {envelope.Message.GetType()}:{envelope.Message}");
            });

        //just wait for started message to be processed to make the output look less confusing
        Task.Delay(500).Wait();
        root.Send(pid, "hello");

        Task.Delay(500).Wait();
        Console.WriteLine(nameof(root.Send) + " done. Press [Enter] to try " + nameof(root.RequestAsync) + ".");
        Console.ReadLine();

        var response = root.RequestAsync<string>(pid, "hello_requested").Result;
        Console.WriteLine("Response was : " + response);

        Console.ReadLine();
    }
}