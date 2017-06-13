
using System;
using System.Threading.Tasks;
using Google.Protobuf;
using Proto;
using Proto.Cluster;
using Proto.Remote;

namespace Messages
{
    public static class Grains
    {
			
        internal static Func<IHelloGrain> _HelloGrainFactory;

        public static void HelloGrainFactory(Func<IHelloGrain> factory) 
        {
            _HelloGrainFactory = factory;
            Remote.RegisterKnownKind("HelloGrain", Actor.FromProducer(() => new HelloGrainActor()));
        } 

        public static HelloGrainClient HelloGrain(string id) => new HelloGrainClient(id);
			
    }

		
    public interface IHelloGrain
    {
		
        Task<HelloResponse> SayHello(HelloRequest request);
		
    }

    public class HelloGrainClient
    {
        private readonly string _id;

        public HelloGrainClient(string id)
        {
            _id = id;
        }

		
        public async Task< HelloResponse> SayHello( HelloRequest request)
        {
            var pid = await Cluster.GetAsync(_id, "HelloGrain");
            var gr = new GrainRequest
            {
                Method = "SayHello",
                MessageData = request.ToByteString()
            };
            var res = await pid.RequestAsync<object>(gr);
            if (res is GrainResponse grainResponse)
            {
                return HelloResponse.Parser.ParseFrom(grainResponse.MessageData);
            }
            if (res is GrainErrorResponse grainErrorResponse)
            {
                throw new Exception(grainErrorResponse.Err);
            }
            throw new NotSupportedException();
        }
		
    }

    public class HelloGrainActor : IActor
    {
        private IHelloGrain _inner;

        public async Task ReceiveAsync(IContext context)
        {
            switch (context.Message)
            {
                case Started _:
                {
                    _inner = Grains._HelloGrainFactory();
                    break;
                }
                case GrainRequest request:
                {
                    switch (request.Method)
                    {
						
                        case "SayHello":
                        {
                            var r = HelloRequest.Parser.ParseFrom(request.MessageData);
                            try
                            {
                                var res = await _inner.SayHello(r);
                                var grainResponse = new GrainResponse
                                {
                                    MessageData = res.ToByteString(),
                                };
                                await context.RespondAsync(grainResponse);
                            }
                            catch (Exception x)
                            {
                                var grainErrorResponse = new GrainErrorResponse
                                {
                                    Err = x.ToString()
                                };
                                await context.RespondAsync(grainErrorResponse);
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

