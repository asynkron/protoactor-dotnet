
using System;
using System.Threading;
using System.Threading.Tasks;
using Proto;
using Proto.Cluster;

namespace Cluster.HelloWorld.Messages
{
    public static class Grains
    {
        public static class Factory<T>
        {
            public static Func<T> Create;
        }
        
        public static (string,Props)[] GetClusterKinds()  => new[] { 
                ("HelloGrain", Props.FromProducer(() => new HelloGrainActor())),
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
            var gr = new GrainRequestMessage(0, request);
            //request the RPC method to be invoked
            var res = await _cluster.RequestAsync<object>(_id, "HelloGrain", gr, ct);

            return res switch
            {
                // normal response
                GrainResponseMessage grainResponse => (HelloResponse)grainResponse.ResponseMessage,
                // error response
                GrainErrorResponse grainErrorResponse => throw new Exception(grainErrorResponse.Err),
                // unsupported response
                _ => throw new NotSupportedException()
            };
        }
    }

    class HelloGrainActor : IActor
    {
        private IHelloGrain _inner;

        public async Task ReceiveAsync(IContext context)
        {
            switch (context.Message)
            {
                case ClusterInit msg: 
                {
                    _inner = Grains.Factory<IHelloGrain>.Create();
                    context.SetReceiveTimeout(TimeSpan.FromSeconds(30));
                    break;
                }
                case ReceiveTimeout:
                {
                    context.Stop(context.Self!);
                    break;
                }
                case GrainRequestMessage(var methodIndex, var r):
                {
                    switch (methodIndex)
                    {
                        case 0:
                        {                            
                            try
                            {
                                var res = await _inner.SayHello((HelloRequest)r);
                                var response = new GrainResponseMessage(res);                                
                                context.Respond(response);
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

