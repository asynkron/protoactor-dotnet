using AspNetGrains.Messages;
using Proto;

namespace Node2;

public class HelloGrain : HelloGrainBase
{
    private readonly string _identity;
    private int _count;

    public HelloGrain(IContext ctx, string identity) : base(ctx)
    {
        _identity = identity;
    }

    public override Task<HelloResponse> SayHello(HelloRequest request)
    {
        _count++;
        Console.WriteLine($"Got request!! {_count}");

        var res = new HelloResponse
        {
            Message = $"Hello from typed grain {_identity} Call count {_count}"
        };

        return Task.FromResult(res);
    }
}