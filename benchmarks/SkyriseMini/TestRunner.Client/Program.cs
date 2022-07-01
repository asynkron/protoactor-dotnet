using System.Reflection;
using MassTransit;
using Microsoft.AspNetCore.Mvc;
using Serilog;
using TestRunner.Contract;

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
    
    builder.Services.AddMassTransit(busCfg =>
    {
        if (builder.Environment.IsDevelopment())
        {
            busCfg.UsingRabbitMq((ctx, cfg) =>
            {
                cfg.Host(builder.Configuration["Bus:RabbitMq:Host"], host =>
                {
                    host.Username(builder.Configuration["Bus:RabbitMq:User"]);
                    host.Password(builder.Configuration["Bus:RabbitMq:Password"]);
                });
            });                
        }
        else
        {
            busCfg.UsingAzureServiceBus(
                (_, cfg) =>
                    cfg.Host(builder.Configuration["Bus:ServiceBusConnectionString"]));
        }
    });

    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen();

    var app = builder.Build();

    app.UseSwagger();
    app.UseSwaggerUI();


    app.MapPost("/runMessagingTest",
        (HttpContext _, IPublishEndpoint publisher, [FromQuery] int parallelism, [FromQuery] int durationInSeconds)
            => publisher.Publish(new RunMessagingTest(parallelism, durationInSeconds)));

    app.MapPost("/runActivationTest",
        (HttpContext _, IPublishEndpoint publisher, [FromQuery] int activationCount, [FromQuery] int parallelism)
            => publisher.Publish(new RunActivationTest(activationCount, parallelism)));


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