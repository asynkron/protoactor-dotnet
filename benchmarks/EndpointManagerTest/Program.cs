using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Proto;
using Proto.Remote;
using Proto.Remote.GrpcNet;

namespace EndpointManagerTest;

// This program tests lockup issues with the EndpointManager.
// TL:DR; This is to demonstrate the issue with the locking and blocking waits in EndpointManager, and to confirm the fix.
//
// This recreates a scenario we were seeing in our production environments. 
// What we saw was 30 cluster clients were sending many messages to 2 of the cluster members, who were sending messages to eachother depending
// on actor placement. If something happens and the 2 members had to reboot, they would end up locking up, not being able to do anything.
// This scenario has been recreated more simply here, where you have 2 members sending many messages back and forth, a disconnect comes through
// from a member that recently restarted, and new connections are being opened to other members. Putting all these together, we end up in a situation
// where many threads get stuck at a lock in EndpointManager, while the one thread inside of the lock is waiting for a ServerConnector to stop.
// NOTE: that this can be a bit flakey as we are trying to reproduce a complete thread lockup. So there is a dockerfile to run it in a more consistent
// environment. Using `--cpus="1"` with docker will make it even more consistent, but sometimes it takes a few tries to repro. 
// You will know you reproduced it when you stop seeing "This should log every second." every second. you may also see the built in
// "ThreadPool is running hot" log, but the absence of that log is ambiguous, since if it's locked up it won't finish to log how long it took!
// The other indicator is that all the new connections made at the end should be logging terminations and reconnects and quickly give up (since they don't exist),
// but of course that won't be happening when you're locked up. Also seeing any "terminating" messages without a corresponding "terminated" message
// also indicates that you're locked up.
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
						.AddFilter("Proto.Diagnostics.DiagnosticsStore", LogLevel.Warning)
						.AddFilter("Proto.Remote.ServerConnector", LogLevel.Error)
						.AddSimpleConsole(o => o.SingleLine = true)
			)
		);
		
		var logger = Log.CreateLogger("Main");
		
		_ = Task.Factory.StartNew(async () =>
		{
			while (true)
			{
				try
				{
					await Task.Factory.StartNew(async () => { await Task.Yield(); });
				}
				catch (Exception)
				{
				}

				logger.LogInformation("This should log every second [pending: {pendingWorkItems}].", ThreadPool.PendingWorkItemCount);
				await Task.Delay(1000);
			}
		});
		
		var sys1 = new ActorSystem().WithRemote(GrpcNetRemoteConfig.BindTo("localhost", 12000).WithRemoteKind("noop", Props.FromProducer(() => new NoopActor())));
		await sys1.Remote().StartAsync();
		
		var sys2 = new ActorSystem().WithRemote(GrpcNetRemoteConfig.BindTo("localhost", 12001).WithRemoteKind("noop", Props.FromProducer(() => new NoopActor())));
		await sys2.Remote().StartAsync();
		
		var echoActorOn2 = (await sys1.Remote().SpawnAsync("localhost:12001", "noop", TimeSpan.FromSeconds(1))).Pid;
		_ = Task.Factory.StartNew(async () =>
		{
			while (true)
			{
				for (var i = 0; i < 200; i++)
				{
					_ = sys1.Root.RequestAsync<Touched>(echoActorOn2, new Touch());
				}
				await Task.Yield();
			}
		});
		
		var echoActorOn1 = (await sys2.Remote().SpawnAsync("localhost:12000", "noop", TimeSpan.FromSeconds(1))).Pid;
		_ = Task.Factory.StartNew(async () =>
		{
			while (true)
			{
				for (var i = 0; i < 200; i++)
				{
					_ = sys2.Root.RequestAsync<Touched>(echoActorOn1, new Touch());
				}
				await Task.Yield();
			}
		});
		
		await Task.Delay(3000);
		
		sys1.EventStream.Publish(new EndpointTerminatedEvent(false, "localhost:12001", null));
		
		var port = 12002;
		for (var i = 12002; i < 12032; i++)
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