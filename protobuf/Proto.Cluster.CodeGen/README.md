# Proto.Actor Grains Generator

The tool can generate C# code for Proto.Actor grains from Protobuf RPC contracts.

## Example

For example, take the following Protobuf contract file:

```proto
syntax = "proto3";
package messages;
option csharp_namespace = "Messages";

message HelloRequest {}
message HelloResponse {
    string Message=1;
}

service HelloGrain {
	rpc SayHello(HelloRequest) returns (HelloResponse) {}
}
```

When you run `protograin many *.proto` in the proto file directory, you will get the following code file:

```csharp
using System;
using System.Threading;
using System.Threading.Tasks;
using Google.Protobuf;
using Proto;
using Proto.Cluster;
using Proto.Remote;

namespace Messages
{
    public class Grains
    {
        public Cluster Cluster { get; }

        public Grains(Cluster cluster) => Cluster = cluster;

        internal Func<string, IHelloGrain> GetHelloGrain { get; private set; }

        public void HelloGrainFactory(Func<string, IHelloGrain> factory) 
        {
            GetHelloGrain = factory;
            Cluster.Remote.RegisterKnownKind("HelloGrain", Props.FromProducer(() => new HelloGrainActor(this)));
        } 

        public void HelloGrainFactory(Func<IHelloGrain> factory) => HelloGrainFactory(id => factory());

        public HelloGrainClient HelloGrain(string id) => new HelloGrainClient(Cluster, id);
    }

    public interface IHelloGrain
    {
        Task<HelloResponse> SayHello(HelloRequest request);
    }

    public class HelloGrainClient
    {
        private readonly string _id;
        private readonly Cluster _cluster;

        public HelloGrainClient(Cluster cluster, string id)
        {
            _id = id;
            _cluster = cluster;
        }

        public Task<HelloResponse> SayHello(HelloRequest request) => SayHello(request, CancellationToken.None);

        public async Task<HelloResponse> SayHello(HelloRequest request, CancellationToken ct, GrainCallOptions options = null)
        {
            options ??= GrainCallOptions.Default;
            
            var gr = new GrainRequest
            {
                MethodIndex = 0,
                MessageData = request.ToByteString()
            };

            async Task<HelloResponse> Inner() 
            {
                //resolve the grain
                var (pid, statusCode) = await _cluster.GetAsync(_id, "HelloGrain", ct);

                if (statusCode != ResponseStatusCode.OK)
                {
                    throw new Exception($"Get PID failed with StatusCode: {statusCode}");  
                }

                //request the RPC method to be invoked
                var res = await _cluster.System.Root.RequestAsync<object>(pid, gr, ct);

                return res switch
                {
                    //did we get a response?
                    GrainResponse grainResponse => HelloResponse.Parser.ParseFrom(grainResponse.MessageData),
                    //did we get an error response?
                    GrainErrorResponse grainErrorResponse => throw new Exception(grainErrorResponse.Err),
                    _                                     => throw new NotSupportedException()
                };
            }

            for (int i = 0; i < options.RetryCount; i++)
            {
                try
                {
                    return await Inner();
                }
                catch (Exception)
                {
                    if (options.RetryAction is not null)
                    {
                        await options.RetryAction(i);
                    }
                }
            }
            return await Inner();
        }
    }

    public class HelloGrainActor : IActor
    {
        private IHelloGrain _inner;
        private readonly Grains _grains;

        public HelloGrainActor(Grains grains) => _grains = grains;

        public async Task ReceiveAsync(IContext context)
        {
            switch (context.Message)
            {
                case Started _:
                {
                    _inner = _grains.GetHelloGrain(context.Self!.Id);
                    context.SetReceiveTimeout(TimeSpan.FromSeconds(30));
                    break;
                }
                case ReceiveTimeout _:
                {
                    context.Stop(context.Self!);
                    break;
                }
                case GrainRequest request:
                {
                    switch (request.MethodIndex)
                    {
                        case 0:
                        {
                            var r = HelloRequest.Parser.ParseFrom(request.MessageData);
                            try
                            {
                                var res = await _inner.SayHello(r);
                                var grainResponse = new GrainResponse
                                {
                                    MessageData = res.ToByteString(),
                                };
                                context.Respond(grainResponse);
                            }
                            catch (Exception x)
                            {
                                var grainErrorResponse = new GrainErrorResponse
                                {
                                    Err = x.ToString()
                                };
                                context.Respond(grainErrorResponse);
                            }

                            break;
                        }
                    }

                    break;
                }
            }
        }
    }
}
```