// -----------------------------------------------------------------------
// <copyright file="ChannelWriterActor.cs" company="Asynkron AB">
//      Copyright (C) 2015-2021 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------
using System;
using System.Threading.Channels;
using System.Threading.Tasks;
using JetBrains.Annotations;

namespace Proto.Channels
{
    [PublicAPI]
    public class ChannelSubscriberActor<T> : IActor
    {
        public static void StartNew(IRootContext context, PID publisher, Channel<T> channel)
        {
            var props = Props.FromProducer(() => new ChannelSubscriberActor<T>(publisher, channel));
            var pid = context.Spawn(props);
        }

        private readonly PID _publisher;
        private readonly Channel<T> _channel;

        public ChannelSubscriberActor(PID publisher, Channel<T> channel)
        {
            _publisher = publisher;
            _channel = channel;
        }

        public async Task ReceiveAsync(IContext context)
        {
            if (context.Message is Started)
            {
                context.Request(_publisher, context.Self);
            }
            if (context.Message is T typed)
            {
                await _channel.Writer.WriteAsync(typed);
            }
        }
    }
}