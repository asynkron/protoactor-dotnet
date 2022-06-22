// -----------------------------------------------------------------------
// <copyright file="ChannelWriterActor.cs" company="Asynkron AB">
//      Copyright (C) 2015-2022 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------
using System.Threading.Channels;
using System.Threading.Tasks;
using JetBrains.Annotations;

namespace Proto.Channels;

[PublicAPI]
public static class ChannelSubscriber
{
    /// <summary>
    ///  Starts a new subscriber actor, that subscribes to messages from <see cref="ChannelPublisherActor{T}"/>.
    /// Received messages will be sent to the specified channel.
    /// </summary>
    /// <param name="context">The parent context used to spawn</param>
    /// <param name="publisher">The PID of the publisher actor to subscribe to</param>
    /// <param name="channel">The channel to write messages to</param>
    /// <typeparam name="T">The Type of channel elements</typeparam>
    /// <returns></returns>
    public static PID StartNew<T>(IRootContext context, PID publisher, Channel<T> channel)
    {
        var props = Props.FromProducer(() => new ChannelSubscriberActor<T>(publisher, channel));
        var pid = context.Spawn(props);
        return pid;
    }
}

[PublicAPI]
public class ChannelSubscriberActor<T> : IActor
{
    private readonly Channel<T> _channel;
    private readonly PID _publisher;

    public ChannelSubscriberActor(PID publisher, Channel<T> channel)
    {
        _publisher = publisher;
        _channel = channel;
    }

    public async Task ReceiveAsync(IContext context)
    {
        switch (context.Message)
        {
            case Started:
                context.Watch(_publisher);
                context.Request(_publisher, context.Self);
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