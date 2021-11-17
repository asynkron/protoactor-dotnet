using Microsoft.Extensions.Logging;
using Proto;
using Proto.Remote;
using Proto.Remote.GrpcNet;
using RemoteCommunication;

var loggerFactory = LoggerFactory.Create(c => c.SetMinimumLevel(LogLevel.Trace).AddFilter("Microsoft", LogLevel.None).AddFilter("Grpc", LogLevel.None).AddConsole());
Log.SetLoggerFactory(loggerFactory);

//The client connects to servers and communicate using only bidirectional grpc streams
var client = new ActorSystem().WithClientRemote(GrpcNetRemoteConfig.BindToLocalhost());
await client.Remote().StartAsync();

//NodeB communicate using pairs of bidirectional grpc streams with other servers and responds to clients using the EndpointReader
var nodeB = new ActorSystem().WithRemote(GrpcNetRemoteConfig.BindToLocalhost(13001));

await nodeB.Remote().StartAsync();
var responderOnNodeB = nodeB.Root.SpawnNamed(Props.FromProducer(_ => new Responder()), "responder");

//NodeA communicate using pairs of bidirectional grpc streams with other servers and responds to clients using the EndpointReader
var nodeA = new ActorSystem().WithRemote(GrpcNetRemoteConfig.BindToLocalhost(13000));

await nodeA.Remote().StartAsync();

var forwarderOnNodeA = nodeA.Root.SpawnNamed(Props.FromProducer(_ => new Forwarder(responderOnNodeB.Clone())), "forwarder").Clone();
var clientRoot = client.Root;
var responseFromResponder = await clientRoot.RequestAsync<Pong>(responderOnNodeB.Clone(), new Ping(), CancellationToken.None);

clientRoot.SpawnNamed(Props.FromProducer(_ => new ClientActor(forwarderOnNodeA.Clone(), responderOnNodeB.Clone())), "ClientActor");


var responseFromResponderThroughForwarderOnNodeA = await clientRoot.RequestAsync<Pong>(forwarderOnNodeA.Clone(), new Ping(), CancellationToken.None);

var pidToWatch = clientRoot.SpawnNamed(Props.FromFunc(m => Task.CompletedTask), "Test");
var result = await clientRoot.RequestAsync<string>(responderOnNodeB.Clone(), pidToWatch.Clone());
await clientRoot.StopAsync(pidToWatch.Clone());
await Task.Delay(500);
result = await clientRoot.RequestAsync<string>(responderOnNodeB.Clone(), pidToWatch.Clone());
if (result != $"{responderOnNodeB}: {pidToWatch} was stopped")
    throw new Exception("Error");

await clientRoot.StopAsync(forwarderOnNodeA.Clone());
await clientRoot.StopAsync(responderOnNodeB.Clone());

Log.CreateLogger("Program").LogInformation("Stopping client");
await client.Remote().ShutdownAsync();
Log.CreateLogger("Program").LogInformation("Stopping nodeA");
await nodeA.Remote().ShutdownAsync();
Log.CreateLogger("Program").LogInformation("Stopping nodeB");
await nodeB.Remote().ShutdownAsync();