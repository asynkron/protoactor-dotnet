using System.Configuration;
using System.Reflection;
using AkkaSut.Shared;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OrleansSut.Shared;
using ProtoActorSut.Shared;
using Serilog;
using TestRunner.Akka;
using TestRunner.Bus;
using TestRunner.Dapr;
using TestRunner.Monitoring;
using TestRunner.Orleans;
using TestRunner.ProtoActor;
using TestRunner.Tests;

var builder = WebApplication.CreateBuilder(args);

try
{
    builder.Host.UseSerilog((_, lcfg) =>
        lcfg
            .ReadFrom.Configuration(builder.Configuration)
            .WriteTo.Console()
            .WriteTo.Seq(builder.Configuration["SeqUrl"])
            .Enrich.WithMachineName()
            .Enrich.WithProperty("Service", Assembly.GetExecutingAssembly().GetName().Name));

    var actorFramework = builder.Configuration["ActorFramework"];

    switch (actorFramework)
    {
        case "Orleans":
            builder
                .AddOrleans()
                .AddOrleansTestServices();
            break;
        case "ProtoActor":
            builder
                .AddProtoActor()
                .AddProtoActorTestServices();
            break;
        case "ProtoActorRaw":
            builder
                .AddProtoActor()
                .AddProtoActorTestServicesRaw();
            break;
        case "Akka":
            builder
                .AddAkkaClusterSharding()
                .AddAkkaClusterProxyHostedService()
                .AddAkkaTestServices();
            break;
        case "Dapr":
            builder.AddDaprTestServices();
            break;
        default:
            throw new ConfigurationErrorsException("Unknown framework " + actorFramework);
    }

    builder.AddMassTransit();

    builder.Services.AddSingleton<TestManager>();
    builder.Services.AddTransient<MessagingTest>();
    builder.Services.AddTransient<ActivationTest>();

    builder.Services.AddOpenTelemetryMetrics(b => b
            .SetResourceBuilder(
                ResourceBuilder.CreateDefault()
                    .AddService(Assembly.GetExecutingAssembly().GetName().Name, serviceInstanceId: Environment.MachineName))
            .AddTestMetrics()
            .AddPrometheusExporter(cfg => cfg.ScrapeResponseCacheDurationMilliseconds = 1000)
        );

    var app = builder.Build();

    app.UseRouting();
    app.UseOpenTelemetryPrometheusScrapingEndpoint();
    
    app.Run();
}
catch (Exception e)
{
    Log.Logger.Fatal(e, "Service crash");
    throw;
}
finally
{
    Log.CloseAndFlush();
}