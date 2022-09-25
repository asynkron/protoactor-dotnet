// -----------------------------------------------------------------------
// <copyright file="ChannelWriterActor.cs" company="Asynkron AB">
//      Copyright (C) 2015-2022 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------

using System.Threading.Channels;
using System.Threading.Tasks;
using Proto;

namespace Common;

public static class ChannelSubscriber
{
    /// <summary>
    ///     Starts a new subscriber actor, that subscribes to messages from <see cref="ChannelPublisherActor{T}" />.
    ///     Received messages will be sent to the specified channel.
    /// </summary>
    /// <param name="context">The parent context used to spawn</param>
    /// <param name="publisher">The PID of the publisher actor to subscribe to</param>
    /// <param name="channel">The channel to write messages to</param>
    /// <typeparam name="T">The Type of channel elements</typeparam>
    /// <returns></returns>
    public static async Task<PID> StartNew<T>(IRootContext context, PID publisher, Channel<T> channel)
    {
        var tcs = new TaskCompletionSource();
        var props = Props.FromProducer(() => new ChannelSubscriberActor<T>(publisher, channel, tcs));
        var pid = context.Spawn(props);

        await tcs.Task;

        return pid;
    }
}

public class ChannelSubscriberActor<T> : IActor
{
    private readonly Channel<T> _channel;
    private readonly PID _publisher;
    private readonly TaskCompletionSource _subscribed;

    public ChannelSubscriberActor(PID publisher, Channel<T> channel, TaskCompletionSource subscribed)

    {
        _publisher = publisher;
        _channel = channel;
        _subscribed = subscribed;
    }

    public async Task ReceiveAsync(IContext context)
    {
        switch (context.Message)
        {
            case Started:
                context.Watch(_publisher);
                context.Request(_publisher, context.Self);

                break;

            case Subscribed:
                _subscribed.SetResult();

                break;

            case Stopping:
                _channel.Writer.Complete();

                break;

            case Terminated t when t.Who.Equals(_publisher):
                _channel.Writer.Complete();

                break;

            case T typed:
                await _channel.Writer.WriteAsync(typed);

                break;
        }
    }
}

public record Subscribed;