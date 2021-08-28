// -----------------------------------------------------------------------
// <copyright file="Channels.cs" company="Asynkron AB">
//      Copyright (C) 2015-2021 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------
using System.Threading.Channels;
using System.Threading.Tasks;

namespace Proto.Channels.Experimental
{
    public static class Channels
    {
        public static PID SpawnPublisherActor<T>(this IRootContext context, string name)
        {
            var props = Props.FromProducer(() => new ChannelPublisherActor<T>());
            var pid = context.SpawnNamed(props, name);
            return pid;
        }
        
        public static PID SubscribeToPid<T>(this Channel<T> channel , IRootContext context, PID publisher)
        {
            var props = Props.FromProducer(() => new ChannelSubscriberActor<T>(publisher, channel));
            var pid = context.Spawn(props);
            return pid;
        }
        
        public static PID PublishToPid<T>(this Channel<T> channel, IRootContext context, PID pid)
        {
            _ = Task.Run(async () => {
                    await foreach (var msg in channel.Reader.ReadAllAsync())
                    {
                        context.Send(pid, msg!);
                    }

                    await context.PoisonAsync(pid);
                }
            );
            return pid;
        }
        
    }
}