// -----------------------------------------------------------------------
// <copyright file="Protos.cs" company="Asynkron AB">
//      Copyright (C) 2015-2020 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------
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
        public Grains(Cluster cluster)
        {
            Cluster = cluster;
        }

        private Cluster Cluster { get; }

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
        private readonly Cluster _cluster;
        private readonly string _id;

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
        private readonly Grains _grains;
        private IHelloGrain _inner;

        public HelloGrainActor(Grains grains)
        {
            _grains = grains;
        }

        protected string Identity { get; private set; }

        protected string Kind { get; private set; }

        public async Task ReceiveAsync(IContext context)
        {
            switch (context.Message)
            {
                case ClusterInit msg:
                {
                    _inner = _grains.GetHelloGrain();
                    context.SetReceiveTimeout(TimeSpan.FromSeconds(30));
                    Identity = msg.Identity;
                    Kind = msg.Kind;
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
                                    MessageData = res.ToByteString()
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