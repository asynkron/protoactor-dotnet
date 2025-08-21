using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Proto;
using Proto.OpenTelemetry;

// Configure logging so Proto.Actor writes to the console
var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
Log.SetLoggerFactory(loggerFactory);

// Export spans and metrics to the console and an OTLP collector (e.g., TraceLens)
using var tracerProvider = Sdk.CreateTracerProviderBuilder()
    .SetResourceBuilder(ResourceBuilder.CreateDefault().AddService("ActorOpenTelemetrySample"))
    .AddProtoActorInstrumentation()
    .AddConsoleExporter()
    .AddOtlpExporter()
    .Build();

using var meterProvider = Sdk.CreateMeterProviderBuilder()
    .SetResourceBuilder(ResourceBuilder.CreateDefault().AddService("ActorOpenTelemetrySample"))
    .AddProtoActorInstrumentation()
    .AddConsoleExporter()
    .AddOtlpExporter()
    .Build();

var system = new ActorSystem(ActorSystemConfig.Setup().WithMetrics());

// Simple actor that prints its trace id
var props = Props.FromFunc(ctx =>
{
    if (ctx.Message is string msg)
    {
        Console.WriteLine($"[actor] received '{msg}' traceId={Activity.Current?.TraceId}");
    }

    return Task.CompletedTask;
}).WithTracing();

var pid = system.Root.Spawn(props);
var tracedRoot = system.Root.WithTracing();

using var rootActivity = new ActivitySource(Proto.ProtoTags.ActivitySourceName).StartActivity("root");
tracedRoot.Send(pid, "hello");

// Flush telemetry before exiting
tracerProvider.ForceFlush();
meterProvider.ForceFlush();

Console.WriteLine("Press enter to exit");
Console.ReadLine();
