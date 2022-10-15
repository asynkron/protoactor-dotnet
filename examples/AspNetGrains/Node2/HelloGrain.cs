using AspNetGrains.Messages;
using Proto;

namespace Node2;

public class HelloGrain : HelloGrainBase
{
    private readonly string _identity;

    public HelloGrain(IContext ctx, string identity) : base(ctx)
    {
        _identity = identity;
    }

    public override Task<HelloResponse> SayHello(HelloRequest request)
    {
        Console.WriteLine("Got request!!");

        var res = new HelloResponse
        {
            Message = $"Hello from typed grain {_identity}"
        };

        return Task.FromResult(res);
    }
}