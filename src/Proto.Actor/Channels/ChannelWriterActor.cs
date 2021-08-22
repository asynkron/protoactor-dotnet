// -----------------------------------------------------------------------
// <copyright file="ChannelWriterActor.cs" company="Asynkron AB">
//      Copyright (C) 2015-2021 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------
using System;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace Proto.Channels
{
    public class ChannelWriterActor<T> : IActor
    {
        public static Props Props(Channel<T> channel) => Proto.Props.FromProducer(() => new ChannelWriterActor<T>(channel));
        
        private readonly Channel<T> _channel;

        public ChannelWriterActor(Channel<T> channel) => _channel = channel;

        public async Task ReceiveAsync(IContext context)
        {
            if (context.Message is T typed)
            {
                await _channel.Writer.WriteAsync(typed);
            }
        }
    }
}