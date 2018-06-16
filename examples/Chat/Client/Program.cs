using System;
using chat.messages;
using Jaeger;
using Jaeger.Samplers;
using OpenTracing.Util;
using Proto;
using Proto.OpenTracing;
using Proto.Remote;

class Program
{
    static void Main(string[] args)
    {
        var tracer = new Tracer.Builder("Proto.Chat.Client")
            .WithSampler(new ConstSampler(true))
            .Build();

        SpanSetup spanSetup = (span, message) => span.Log(message?.ToString());
        var openTracingMiddleware = OpenTracingExtensions.OpenTracingSenderMiddleware(tracer);

        Serialization.RegisterFileDescriptor(ChatReflection.Descriptor);
        Remote.Start("127.0.0.1", 0);
        var server = new PID("127.0.0.1:8000", "chatserver");
        var context = new RootContext(default, openTracingMiddleware);

        var props = Props.FromFunc(ctx =>
        {
            switch (ctx.Message)
            {
                case Connected connected:
                    Console.WriteLine(connected.Message);
                    break;
                case SayResponse sayResponse:
                    Console.WriteLine($"{sayResponse.UserName} {sayResponse.Message}");
                    break;
                case NickResponse nickResponse:
                    Console.WriteLine($"{nickResponse.OldUserName} is now {nickResponse.NewUserName}");
                    break;
            }
            return Actor.Done;
        })
        .WithOpenTracing(spanSetup, spanSetup, tracer);

        var client = context.Spawn(props);
        context.Send(server, new Connect
        {
            Sender = client
        });
        var nick = "Alex";
        while (true)
        {
            var text = Console.ReadLine();
            if (text.Equals("/exit"))
            {
                return;
            }
            if (text.StartsWith("/nick "))
            {
                var t = text.Split(' ')[1];
                context.Send(server, new NickRequest
                {
                    OldUserName = nick,
                    NewUserName = t
                });
                nick = t;
            }
            else
            {
                context.Send(server, new SayRequest
                {
                    UserName = nick,
                    Message = text
                });
            }
        }
    }
}
