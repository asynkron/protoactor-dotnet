// -----------------------------------------------------------------------
// <copyright file="ChannelReaderActor.cs" company="Asynkron AB">
//      Copyright (C) 2015-2022 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------

using System.Collections.Generic;
using System.Threading.Channels;
using System.Threading.Tasks;
using Proto;

namespace Common;

public static class ChannelPublisher
{
    /// <summary>
    ///     Starts a new channel publisher actor. This actor will read from the given channel
    ///     and send the received messages to the subscribers. The actor will poison itself when the channel is closed.
    /// </summary>
    /// <remarks>Use <see cref="ChannelSubscriber.StartNew{T}" /> to subscribe</remarks>
    /// <param name="context">The parent context used to spawn the actor</param>
    /// <param name="channel">The source channel</param>
    /// <param name="name">The name of the publisher actor</param>
    /// <typeparam name="T">The Type of the channel elements</typeparam>
    /// <returns></returns>
    public static PID StartNew<T>(IRootContext context, Channel<T> channel, string name)
    {
        var props = Props.FromProducer(() => new ChannelPublisherActor<T>());
        var pid = context.SpawnNamed(props, name);

        _ = Task.Run(async () =>
            {
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
                context.Respond(new Subscribed());

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