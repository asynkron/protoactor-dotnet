using Microsoft.Extensions.Logging;
using Proto;
using Proto.Remote;
using Proto.Remote.GrpcNet;

namespace EndpointManagerTest;

class Program
{
	private static async Task Main()
	{
		Log.SetLoggerFactory(
			LoggerFactory.Create(
				c =>
					c.SetMinimumLevel(LogLevel.Debug)
						.AddFilter("Microsoft", LogLevel.None)
						.AddFilter("Grpc", LogLevel.None)
						.AddFilter("Proto.Context.ActorContext", LogLevel.Information)
						.AddFilter("Proto.Remote.ServerConnector", LogLevel.Error)
						.AddSimpleConsole(o => o.SingleLine = true)
			)
		);
		
		var logger = Log.CreateLogger("Main");
		
		var sys1 = new ActorSystem().WithRemote(GrpcNetRemoteConfig.BindTo("localhost", 12000).WithRemoteKind("noop", Props.FromProducer(() => new NoopActor())));
		await sys1.Remote().StartAsync();
		
		var sys2 = new ActorSystem().WithRemote(GrpcNetRemoteConfig.BindTo("localhost", 12001).WithRemoteKind("noop", Props.FromProducer(() => new NoopActor())));
		await sys2.Remote().StartAsync();
		
		var echoActorOn2 = (await sys1.Remote().SpawnAsync("localhost:12001", "noop", TimeSpan.FromSeconds(1))).Pid;
		_ = Task.Factory.StartNew(async () =>
		{
			while (true)
			{
				_ = sys1.Root.RequestAsync<Touched>(echoActorOn2, new Touch());
			}
		});
		
		var echoActorOn1 = (await sys2.Remote().SpawnAsync("localhost:12000", "noop", TimeSpan.FromSeconds(1))).Pid;
		_ = Task.Factory.StartNew(async () =>
		{
			while (true)
			{
				_ = sys2.Root.RequestAsync<Touched>(echoActorOn1, new Touch());
			}
		});
		
		await Task.Delay(3000);
		
		sys1.EventStream.Publish(new EndpointTerminatedEvent(false, "localhost:12001", null));
		
		var port = 12002;
		for (var i = 12002; i < 12012; i++)
		{
			//logger.LogInformation("Touching {i}", i);
			_ = sys1.Root.RequestAsync<Touched>(new PID($"localhost:{i}", "$1"), new Touch());
		}
		
		while (true)
		{
			//logger.LogInformation("End");
			await Task.Delay(1000);
		}
	}
}

public class NoopActor : IActor
{	
	public async Task ReceiveAsync(IContext context)
	{
	}
}