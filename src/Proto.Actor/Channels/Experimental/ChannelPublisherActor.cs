// -----------------------------------------------------------------------
// <copyright file="ChannelReaderActor.cs" company="Asynkron AB">
//      Copyright (C) 2015-2021 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------
using System.Collections.Generic;
using System.Threading.Channels;
using System.Threading.Tasks;
using JetBrains.Annotations;

namespace Proto.Channels.Experimental
{
    [PublicAPI]
    public class ChannelPublisherActor<T> : IActor
    {
        private readonly HashSet<PID> _subscribers = new();

        public Task ReceiveAsync(IContext context)
        {
            switch (context.Message)
            {
                case Stopping:
                    foreach (var sub in _subscribers)
                    {
                        context.Poison(sub);
                    }
                    break;
                case PID subscriber:
                    _subscribers.Add(subscriber);
                    context.Watch(subscriber);
                    break;
                case Terminated terminated:
                    _subscribers.Remove(terminated.Who);
                    break;
                case T typed:
                    foreach (var sub in _subscribers)
                    {
                        context.Send(sub, typed);
                    }
                    break;
            }
            return Task.CompletedTask;
        }
    }
}