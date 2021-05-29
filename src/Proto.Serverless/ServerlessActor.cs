// -----------------------------------------------------------------------
// <copyright file="ServerlessActor.cs" company="Asynkron AB">
//      Copyright (C) 2015-2021 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------
using System;
using System.Threading.Tasks;
using Google.Protobuf;
using Google.Protobuf.WellKnownTypes;
using Proto.Cluster;

namespace Proto.Serverless
{
    public class ServerlessActor : IActor
    {
        private string _kind;
        private string _identity;
        private Any _state = null;
        private readonly Serverless.ServerlessClient _client;

        public ServerlessActor(Serverless.ServerlessClient client)
        {
            _client = client;
        }

        public Task ReceiveAsync(IContext context) => context.Message switch
        {
            Started _    => OnStarted(context),
            IMessage msg => OnMessage(context, msg),
            _            => Task.CompletedTask,
        };

        private async Task OnMessage(IContext context, IMessage request)
        {
            var response = await _client.ReceiveAsync(new ReceiveRequest
                {
                    Identity = _identity,
                    Kind = _kind,
                    State = _state,
                    Request = Any.Pack(request),
                }
            );

            _state = response.State;
            var r = response.Response;
            //How to handle here?

            context.Respond(r);
        }

        private Task OnStarted(IContext context)
        {
            var ci = context.Get<ClusterIdentity>()!;
            _identity = ci.Identity;
            _kind = ci.Kind;

            return Task.CompletedTask;
        }
    }
}