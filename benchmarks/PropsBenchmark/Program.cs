//this is not intended to show performance, nor any functionality
//this is only for measuring Props memory consumption

using PropsBenchmark;
using Proto;

var system = new ActorSystem();
var props = Props.FromFunc(ctx => Task.CompletedTask);

Console.WriteLine("Starting");
for (var i = 0; i < 1_000_000; i++)
{
    var i1 = i;
    var pid = system.Root.SpawnNamed(props,"x" + i, ctx => {
        ctx.Set(new SomeState("SomeId" + i1));
    });
}

Console.WriteLine("Done");

Console.ReadLine();