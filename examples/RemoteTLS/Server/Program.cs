using System;
using System.Security.Cryptography.X509Certificates;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Proto;
using Proto.Remote;
using Proto.Remote.GrpcNet;
using static Proto.Remote.RemoteConfig;
using Common;

var certificate = new X509Certificate2("../localhost.pfx", "password");

var remoteConfig = BindToLocalhost(8000) with
{
    UseHttps = true,
    ConfigureKestrel = options =>
    {
        options.Protocols = HttpProtocols.Http2;
        options.UseHttps(certificate);
    }
};

var system = new ActorSystem().WithRemote(remoteConfig);

var props = Props.FromProducer(() => new GreeterActor());
system.Root.SpawnNamed(props, "greeter");

await system.Remote().StartAsync();
Console.WriteLine("Remote TLS server started. Press enter to exit.");
Console.ReadLine();

public class GreeterActor : IActor
{
    public Task ReceiveAsync(IContext context)
        => context.Message switch
        {
            HelloRequest req => context.Respond(new HelloResponse($"Hello, {req.Name}!"))
                .AsTask(),
            _ => Task.CompletedTask
        };
}
