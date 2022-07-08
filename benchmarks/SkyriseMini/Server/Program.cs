using System.Reflection;
using Microsoft.AspNetCore.Builder;
using Serilog;
using SkyriseMini;
using Log = Serilog.Log;

var builder = WebApplication.CreateBuilder(args);

try
{
    builder.Host.UseSerilog((_, lcfg) =>
        lcfg
            .ReadFrom.Configuration(builder.Configuration)
            .WriteTo.Console()
            .WriteTo.Seq(builder.Configuration["SeqUrl"]!)
            .Enrich.WithProperty("Service", Assembly.GetExecutingAssembly().GetName().Name)
    );
    
    Console.WriteLine("Starting server");
    builder.AddProtoActorSUT();
    var app = builder.Build();
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