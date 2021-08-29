// -----------------------------------------------------------------------
// <copyright file="ServerlessActor.cs" company="Asynkron AB">
//      Copyright (C) 2015-2021 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------
using System.Threading.Tasks;
using Google.Protobuf;
using Google.Protobuf.WellKnownTypes;
using Proto.Cluster;
using Proto.Utils;

namespace Proto.Serverless
{
    public class ShapelessActor : IActor
    {
        private string _kind;
        private string _identity;
        private Any _state = null;
        private readonly ProtoServer.ProtoServerClient _client;
        private readonly IKeyValueStore<Any> _store;
        private string _key;

        public ShapelessActor(ProtoServer.ProtoServerClient client, IKeyValueStore<Any> store)
        {
            _client = client;
            _store = store;
        }

        public Task ReceiveAsync(IContext context) => context.Message switch
        {
            Started _    => OnStarted(context),
            IMessage msg => OnMessage(context, msg),
            _            => Task.CompletedTask,
        };

        private async Task OnMessage(IContext context, IMessage request)
        {
            var response = await _client.ReceiveAsync(new ServerRequest()
                {
                    Identity = _identity,
                    Kind = _kind,
                    State = _state,
                    Request = Any.Pack(request),
                }
            );

            _state = response.State;
            await _store.SetAsync(_key, _state, CancellationTokens.FromSeconds(5));
            var r = response.Response;
            //How to handle here?

            context.Respond(r);
        }

        private async Task OnStarted(IContext context)
        {
            var ci = context.Get<ClusterIdentity>()!;
            _identity = ci.Identity;
            _kind = ci.Kind;
            _key = ci.ToDiagnosticString();

            _state = await _store.GetAsync(_key, CancellationTokens.FromSeconds(5));
        }
    }
}