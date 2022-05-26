//this is not intended to show performance, nor any functionality
//this is only for measuring Props memory consumption

using PropsBenchmark;
using Proto;

var system = new ActorSystem();
var props = Props.FromFunc(ctx => Task.CompletedTask);

Console.WriteLine("Starting");
for (var i = 0; i < 1_000_000; i++)
{
    var p = props.WithOnInit(ctx => {
            ctx.Set(new SomeState("SomeId" + i));
        }
    );

    var pid = system.Root.Spawn(p);
}

Console.WriteLine("Done");

Console.ReadLine();