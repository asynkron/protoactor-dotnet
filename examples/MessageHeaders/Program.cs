using System;
using System.Threading.Tasks;
using Proto;

namespace MessageHeaders
{
    public record MyMessage(string SomeProperty);
    
    class Program
    {
        static void Main()
        {
            var system = new ActorSystem();
            var props = Props.FromFunc(ctx => {
                    if (ctx.Message is MyMessage msg)
                    {
                        Console.WriteLine($"Got message {msg.SomeProperty}");

                        foreach (var (key, value) in ctx.Headers)
                        {
                            Console.WriteLine($"Key: {key}, Value:: {value}");
                        }
                    }

                    return Task.CompletedTask;
                }
            );

            var pid = system.Root.Spawn(props);
            
            var headers = MessageHeader
                .Empty
                .With("UserId", "123")
                .With("LanguageId", "EN");

            //why is this needed?
            //by default, headers are not propagated, which might seem strange at first.
            //but in the case of tracing, e.g. Zipkin/OpenTracing etc. you need to be able to create new spans based on the data in the headers.
            //e.g. RootContext might contain SpanID 123. but on the outgoing data, this is now ParentSpanID 123
            
            //This bit of code does a verbatim copy of all headers and pass them along
            static Sender PropagateHeaders(Sender next) => 
                (context, target, envelope) => 
                    next(context, target, envelope.WithHeader(context.Headers));

            //set up a sender context that knows what headers to pass along.
            var context = system
                .Root
                .WithHeaders(headers)
                .WithSenderMiddleware(PropagateHeaders);
            
            //This is sent with the above specified message headers
            context.Send(pid, new MyMessage("SomeValue"));

            Console.ReadLine();
        }
    }
}