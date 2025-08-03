using System;
using System.Security.Cryptography.X509Certificates;
using Proto;
using Proto.Remote;
using Proto.Remote.GrpcNet;
using static Proto.Remote.GrpcNet.GrpcNetRemoteConfig;
using Common;

var certificate = new X509Certificate2("../localhost.pfx", "password");

var remoteConfig = BindToLocalhost(8000).WithTLS(certificate: certificate);

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
