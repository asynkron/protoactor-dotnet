// -----------------------------------------------------------------------
// <copyright file="DeviceActor.cs" company="Asynkron AB">
//      Copyright (C) 2015-2022 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------
using System.Threading.Tasks;
using KafkaVirtualActorIngress.Messages;
using Proto;
using Proto.Cluster;

namespace KafkaVirtualActorIngress
{
    public class DeviceActor : IActor
    {
        private string _deviceId;
        private DeviceState _state;

        public Task ReceiveAsync(IContext context) => context.Message switch
        {
            Started _           => OnStarted(context),
            SomeMessage sm      => OnSomeMessage(context, sm),
            SomeOtherMessage sm => OnSomeOtherMessage(context, sm),
            _                   => Task.CompletedTask
        };

        private async Task OnSomeOtherMessage(IContext context, SomeOtherMessage sm)
        {
            //TODO: handle SomeMessage
            _state.IntProperty = sm.IntProperty;

            await EvaluateState();
            await SaveState();
            context.Respond(new Ack());
        }

        private async Task OnSomeMessage(IContext context, SomeMessage sm)
        {
            //TODO: handle SomeOtherMessage
            _state.Data = sm.Data;

            await EvaluateState();
            await SaveState();
            context.Respond(new Ack());
        }

        private Task EvaluateState()
        {
            if (!string.IsNullOrEmpty(_state.Data) && _state.IntProperty > 0)
            {
                //This saga is completed...
                //DO stuff
            }

            return Task.CompletedTask;
        }

        private Task SaveState() =>
            //TODO: write _state to some db
            //db.save(_deviceId, _state);
            Task.CompletedTask;

        private async Task OnStarted(IContext ctx)
        {
            _deviceId = ctx.ClusterIdentity()!.Identity;
            _state = await LoadState();
        }

        private async Task<DeviceState> LoadState()
        {
            //TODO: get from database
            //fake db call;
            await Task.Yield();
            return new DeviceState();
        }
    }
}