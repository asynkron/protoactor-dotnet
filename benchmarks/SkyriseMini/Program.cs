using System.Reflection;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using ProtoActorSut.Shared;
using Serilog;
using TestRunner.Tests;
using Log = Serilog.Log;

var builder = WebApplication.CreateBuilder(args);

try
{
    builder.Host.UseSerilog((_, lcfg) =>
        lcfg
            .ReadFrom.Configuration(builder.Configuration)
            .WriteTo.Console()
            .WriteTo.Seq(builder.Configuration["SeqUrl"])
            .Enrich.WithProperty("Service", Assembly.GetExecutingAssembly().GetName().Name));

    builder.Services.AddSingleton<TestManager>();
    builder.Services.AddTransient<MessagingTest>();
    builder.Services.AddTransient<ActivationTest>();
    builder.AddProtoActorTestServicesRaw();
    builder.AddProtoActor();
    

    // builder.Services.AddOpenTelemetryMetrics(b => b
    //         .SetResourceBuilder(
    //             ResourceBuilder.CreateDefault()
    //                 .AddService(Assembly.GetExecutingAssembly().GetName().Name, serviceInstanceId: Environment.MachineName))
    //         .AddTestMetrics()
    //         .AddPrometheusExporter(cfg => cfg.ScrapeResponseCacheDurationMilliseconds = 1000)
    //     );

    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen();
    
    var app = builder.Build();

    app.UseRouting();
    app.UseOpenTelemetryPrometheusScrapingEndpoint();
    
    app.UseSwagger();
    app.UseSwaggerUI();
    
    app.MapPost("/runMessagingTest",
        (HttpContext _,IServiceProvider provider, TestManager manager, [FromQuery] int parallelism, [FromQuery] int durationInSeconds)
            => {
            var test = provider.GetRequiredService<MessagingTest>();
            manager.TrackTest(cancel => test.RunTest(parallelism, durationInSeconds, cancel));

            return Task.CompletedTask;
        }
    );

    app.MapPost("/runActivationTest",
        (HttpContext _,IServiceProvider provider, TestManager manager, [FromQuery] int activationCount, [FromQuery] int parallelism)
            => {
            var test = provider.GetRequiredService<ActivationTest>();
            manager.TrackTest(cancel => test.RunTest(activationCount, parallelism, cancel));

            return Task.CompletedTask;
        }
    );
    
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