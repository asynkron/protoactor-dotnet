using TestRunner.Tests;

namespace TestRunner.ProtoActor;

public static class ProtoActorExtensions
{
    public static WebApplicationBuilder AddProtoActorTestServices(this WebApplicationBuilder builder)
    {
        builder.Services.AddSingleton<ProtoActorTestServices>();
        builder.Services.AddSingleton<Ping>(provider => provider.GetRequiredService<ProtoActorTestServices>().Ping);
        builder.Services.AddSingleton<Activate>(provider => provider.GetRequiredService<ProtoActorTestServices>().Activate);

        return builder;
    }
    
    public static WebApplicationBuilder AddProtoActorTestServicesRaw(this WebApplicationBuilder builder)
    {
        builder.Services.AddSingleton<ProtoActorTestServicesRaw>();
        builder.Services.AddSingleton<Ping>(provider => provider.GetRequiredService<ProtoActorTestServicesRaw>().Ping);
        builder.Services.AddSingleton<Activate>(provider => provider.GetRequiredService<ProtoActorTestServicesRaw>().Activate);

        return builder;
    }
}