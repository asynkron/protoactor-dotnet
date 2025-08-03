using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using OpenTelemetry;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Proto;
using Proto.OpenTelemetry;
using Proto.Remote;
using Proto.Remote.GrpcNet;

var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
Log.SetLoggerFactory(loggerFactory);

using var tracerProvider = Sdk.CreateTracerProviderBuilder()
    .SetResourceBuilder(ResourceBuilder.CreateDefault().AddService("OpenTelemetryTracingSample"))
    .AddProtoActorInstrumentation()
    .AddJaegerExporter()
    .Build();

var system1 = new ActorSystem().WithRemote(GrpcNetRemoteConfig.BindToLocalhost(12000));
var system2 = new ActorSystem().WithRemote(GrpcNetRemoteConfig.BindToLocalhost(12001));

await system1.Remote().StartAsync();
await system2.Remote().StartAsync();

var remoteProps = Props.FromFunc(ctx =>
{
    if (ctx.Message is string msg)
    {
        Console.WriteLine($"[remote] received '{msg}' traceId={Activity.Current?.TraceId}");
        ctx.Respond("pong");
    }
    return Task.CompletedTask;
}).WithTracing();

system2.Root.SpawnNamed(remoteProps, "remote");

var localProps = Props.FromFunc(async ctx =>
{
    if (ctx.Message is string msg)
    {
        Console.WriteLine($"[local] received '{msg}' traceId={Activity.Current?.TraceId}");
        var remotePid = PID.FromAddress("127.0.0.1:12001", "remote");
        var res = await ctx.RequestAsync<string>(remotePid, "ping");
        Console.WriteLine($"[local] got reply '{res}' traceId={Activity.Current?.TraceId}");
    }
}).WithTracing();

var localPid = system1.Root.Spawn(localProps);

var tracedRoot = system1.Root.WithTracing();

using var rootActivity = new ActivitySource(Proto.OpenTelemetry.ProtoTags.ActivitySourceName).StartActivity("root");
tracedRoot.Send(localPid, "start");

Console.WriteLine("Press enter to exit");
Console.ReadLine();
