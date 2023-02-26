using System.Reflection;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Proto;
using Serilog;
using SkyriseMini;
using SkyriseMini.Tests;
using Log = Serilog.Log;

var builder = WebApplication.CreateBuilder(args);

try
{
    builder.Host.UseSerilog((_, lcfg) =>
        lcfg
            .ReadFrom.Configuration(builder.Configuration)
            .WriteTo.Console()
            .WriteTo.Seq(builder.Configuration["SeqUrl"]!)
            .Enrich.WithProperty("Service", Assembly.GetExecutingAssembly().GetName().Name));


    Console.WriteLine("Starting client");
    builder.Services.AddSingleton<TestManager>();
    builder.Services.AddTransient<MessagingTest>();
    builder.Services.AddTransient<ActivationTest>();
    builder.AddProtoActorTestServicesRaw();
    builder.AddProtoActorClient();
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen();

    var app = builder.Build();

    app.UseRouting();
    app.UseSwagger();
    app.UseSwaggerUI();

    app.MapPost("/runMessagingTest",
        (HttpContext _, IServiceProvider provider, TestManager manager, [FromQuery] int parallelism, [FromQuery] int durationInSeconds)
    => {

            var __ = SafeTask.Run( () => {
                    var test = provider.GetRequiredService<MessagingTest>();
                    manager.TrackTest(cancel => test.RunTest(parallelism, durationInSeconds, cancel));
                    return Task.CompletedTask;
                }
            );
            return Task.CompletedTask;
        }
    );

    app.MapPost("/runActivationTest",
        (HttpContext _, IServiceProvider provider, TestManager manager, [FromQuery] int activationCount, [FromQuery] int parallelism)
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