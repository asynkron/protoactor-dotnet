using System.Reflection;
using Proto;
using ProtoActorSut.Contracts;
using ProtoActorSut.Server;
using ProtoActorSut.Shared;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((_, lcfg) =>
    lcfg
        .ReadFrom.Configuration(builder.Configuration)
        .WriteTo.Console()
        .WriteTo.Seq(builder.Configuration["SeqUrl"])
        .Enrich.WithMachineName()
        .Enrich.WithProperty("Service", Assembly.GetExecutingAssembly().GetName().Name));


builder.AddProtoActor(
    (PingPongActorActor.Kind, Props.FromProducer(() => new PingPongActorActor((c, _) => new PingPongActor(c)))),
    (Consts.PingPongRawKind, Props.FromProducer(() => new PingPongActorRaw())));

var app = builder.Build();
app.Run();