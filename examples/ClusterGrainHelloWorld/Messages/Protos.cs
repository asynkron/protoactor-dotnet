
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Google.Protobuf;
using Messages;
using Proto;
using Proto.Cluster;

namespace Cluster.HelloWorld.Messages
{
    public static class Grains
    {
        public static (string,Props)[] GetClusterKinds()  => new[] { 
                ("HelloGrain", Props.FromProducer(() => new HelloGrainActor(null))),
            };
    }        
    
    
    public static class GrainExtensions
    {
        public static HelloGrainClient GetHelloGrain(this Proto.Cluster.Cluster cluster, string identity) => new(cluster, identity);
    }

    public interface IHelloGrain
    {
        Task<HelloResponse> SayHello(HelloRequest request);
    }

    public class HelloGrainClient
    {
        private readonly string _id;
        private readonly Proto.Cluster.Cluster _cluster;

        public HelloGrainClient(Proto.Cluster.Cluster cluster, string id)
        {
            _id = id;
            _cluster = cluster;
        }

        public async Task<HelloResponse> SayHello(HelloRequest request, CancellationToken ct)
        {
            //request the RPC method to be invoked
            var res = await _cluster.RequestAsync<object>(_id, "HelloGrain", gr, ct);

            return res switch
            {
                // normal response
                GrainResponse grainResponse => HelloResponse.Parser.ParseFrom(grainResponse.MessageData),
                // error response
                GrainErrorResponse grainErrorResponse => throw new Exception(grainErrorResponse.Err),
                // unsupported response
                _ => throw new NotSupportedException()
            };
        }
    }

    public class HelloGrainActor : IActor
    {
        private IHelloGrain _inner;
        private readonly Grains _grains;

        public HelloGrainActor(Grains grains) => _grains = grains;
        private string _identity;
        private string _kind;

        protected string Identity => _identity;
        protected string Kind => _kind;

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
                case ClusterInit msg: 
                {
                    _identity = msg.Identity;
                    _kind = msg.Kind;
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

