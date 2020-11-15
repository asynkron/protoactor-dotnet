
using System;
using System.Threading;
using System.Threading.Tasks;
using Google.Protobuf;
using Proto;
using Proto.Cluster;

namespace Messages
{
    public class Grains
    {
        private Cluster Cluster { get; }

        public Grains(Cluster cluster) => Cluster = cluster;

        internal Func<IHelloGrain> GetHelloGrain { get; private set; }

        public void HelloGrainFactory(Func<IHelloGrain> factory) => GetHelloGrain = factory;

        public HelloGrainClient HelloGrain(string id) => new(Cluster, id);
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

        private async Task<HelloResponse> SayHello(HelloRequest request, CancellationToken ct)
        {
            var gr = new GrainRequest
            {
                MethodIndex = 0,
                MessageData = request.ToByteString()
            };

            async Task<HelloResponse> Inner() 
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
            
            return await Inner();
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
                case ClusterInit msg: 
                {
                    _inner = _grains.GetHelloGrain();
                    context.SetReceiveTimeout(TimeSpan.FromSeconds(30));
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

