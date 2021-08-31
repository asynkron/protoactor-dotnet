using System;
using System.Threading.Tasks;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Proto.Serverless;

var port = 808;
var server = new Server
{
    Services = { ProtoServer.BindService(new MyServer(null)) },
    Ports = { new ServerPort("localhost", port, ServerCredentials.Insecure) }
};
server.Start();
Console.ReadKey();
server.ShutdownAsync().Wait();
            
Console.WriteLine("Hello World!");

public class MyServer : ProtoServer.ProtoServerBase
{
    private readonly ProtoClient.ProtoClientClient _client;

    public MyServer(ProtoClient.ProtoClientClient client) => _client = client;

    public override async Task<ServerResponse> Receive(ServerRequest request, ServerCallContext context)
    {
        var msg = request.Request;


        return new ServerResponse
        {
            Response = new Any(),
            State = request.State,
        };
    }
}
