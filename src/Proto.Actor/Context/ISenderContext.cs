// -----------------------------------------------------------------------
// <copyright file="ISenderContext.cs" company="Asynkron AB">
//      Copyright (C) 2015-2022 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Proto.Context;
using Proto.Future;

// ReSharper disable once CheckNamespace
namespace Proto;

public interface ISenderContext : IInfoContext
{
    /// <summary>
    ///     MessageHeaders of the Context
    /// </summary>
    MessageHeader Headers { get; }

    //TODO: should the current message of the actor be exposed to sender middleware?
    object? Message { get; }

    /// <summary>
    ///     Send a message to a given PID target
    /// </summary>
    /// <param name="target">The target PID</param>
    /// <param name="message">The message to send</param>
    void Send(PID target, object message);

    /// <summary>
    ///     Sends a message together with a Sender PID, this allows the target to respond async to the Sender
    /// </summary>
    /// <param name="target">The target PID</param>
    /// <param name="message">The message to send</param>
    /// <param name="sender">Message sender that will receive the response</param>
    void Request(PID target, object message, PID? sender);

    /// <summary>
    ///     Sends a message together with a Sender PID, this allows the target to respond async to the Sender.
    ///     This operation can be awaited.
    /// </summary>
    /// <param name="target">The target PID</param>
    /// <param name="message">The message to send</param>
    /// <param name="cancellationToken">Cancellation token for the request</param>
    /// <typeparam name="T">Expected return message type</typeparam>
    /// <returns>A Task that completes once the Target Responds back to the Sender</returns>
    Task<T> RequestAsync<T>(PID target, object message, CancellationToken cancellationToken);

    /// <summary>
    ///     Get a future handle, to be able to receive a response to requests.
    ///     Dispose when response is received
    /// </summary>
    /// <returns></returns>
    IFuture GetFuture();
}

public static class SenderContextExtensions
{
    private static readonly ILogger Logger = Log.CreateLogger(nameof(SenderContextExtensions));

    /// <summary>
    ///     Creates a batch context for sending a set of requests from the same thread context.
    ///     This is useful if you have several messages which shares a cancellation scope (same cancellationToken).
    ///     It will pre-allocate the number of futures specified and is slightly more efficient on resources than default
    ///     futures.
    ///     If more than the pre-allocated futures are used it will fall back to the default system futures.
    ///     Dispose to release the resources used.
    /// </summary>
    /// <param name="context"></param>
    /// <param name="size">The number of requests to send. The batch context will pre-allocate resources for this</param>
    /// <param name="ct"></param>
    /// <returns></returns>
    public static BatchContext CreateBatchContext(this ISenderContext context, int size, CancellationToken ct) =>
        new(context, size, ct);

    /// <summary>
    ///     Sends a message together with a Sender PID, this allows the target to respond async to the Sender.
    /// </summary>
    /// <param name="self">The context used to issue the request. Response will be sent back to self.Self.</param>
    /// <param name="target">The target PID</param>
    /// <param name="message">The message to send</param>
    public static void Request(this ISenderContext self, PID target, object message) =>
        self.Request(target, message, self.Self);

    /// <summary>
    ///     Sends a message together with a Sender PID, this allows the target to respond async to the Sender.
    ///     This operation can be awaited.
    /// </summary>
    /// <param name="self">the context used to issue the request</param>
    /// <param name="target">The target PID</param>
    /// <param name="message">The message to send</param>
    /// <typeparam name="T">Expected return message type</typeparam>
    /// <returns>A Task that completes once the Target Responds back to the Sender</returns>
    public static Task<T> RequestAsync<T>(this ISenderContext self, PID target, object message) =>
        self.RequestAsync<T>(target, message, self.System.Config.ActorRequestTimeout);

    /// <summary>
    ///     Sends a message together with a Sender PID, this allows the target to respond async to the Sender.
    ///     This operation can be awaited.
    /// </summary>
    /// <param name="self">the context used to issue the request</param>
    /// <param name="target">The target PID</param>
    /// <param name="message">The message to send</param>
    /// <param name="timeout">Timeout for the request</param>
    /// <typeparam name="T">Expected return message type</typeparam>
    /// <returns>A Task that completes once the Target Responds back to the Sender</returns>
    public static async Task<T> RequestAsync<T>(this ISenderContext self, PID target, object message, TimeSpan timeout)
    {
        using var cts = new CancellationTokenSource(timeout);
        var res = await self.RequestAsync<T>(target, message, cts.Token).ConfigureAwait(false);

        return res;
    }

    /// <summary>
    ///     Sends a message together with a Sender PID, this allows the target to respond async to the Sender.
    ///     Once the request completes, the callback is scheduled to run in the actors concurrency constraint.
    /// </summary>
    /// <param name="self">the context used to issue the request</param>
    /// <param name="target">The target PID</param>
    /// <param name="message">The message to send</param>
    /// <param name="callback">Callback gets the request task passed in as a parameter</param>
    /// <param name="ct"></param>
    /// <typeparam name="T">Expected return message type</typeparam>
    public static void RequestReenter<T>(this IContext self, PID target, object message, Func<Task<T>, Task> callback,
        CancellationToken ct)
    {
        var task = self.RequestAsync<T>(target, message, ct);
        self.ReenterAfter(task, callback);
    }

    /// <summary>
    ///     Sends a message together with a Sender PID, this allows the target to respond async to the Sender.
    ///     This operation can be awaited.
    /// </summary>
    /// <param name="self">Calling context</param>
    /// <param name="target">The target PID</param>
    /// <param name="message">The message to send</param>
    /// <param name="headers">Optional headers</param>
    /// <param name="cancellationToken">Optional CancellationToken</param>
    /// <typeparam name="T">Expected return message type</typeparam>
    /// <returns>A Task that completes once the Target Responds back to the Sender</returns>
    public static async Task<(T message, MessageHeader header)> RequestWithHeadersAsync<T>(
        this ISenderContext self,
        PID target,
        object message,
        MessageHeader? headers = null,
        CancellationToken cancellationToken = default
    )
    {
        var request = headers is null ? message : MessageEnvelope.Wrap(message, headers);
        var result = await self.RequestAsync<MessageEnvelope>(target, request, cancellationToken).ConfigureAwait(false);

        var messageResult = MessageEnvelope.UnwrapMessage(result);

        switch (messageResult)
        {
            case null:
            case T:
                return ((T)messageResult!, MessageEnvelope.UnwrapHeader(result));
            default:
                throw new InvalidOperationException(
                    $"Unexpected message. Was type {messageResult.GetType()} but expected {typeof(T)}"
                );
        }
    }

    internal static async Task<T> RequestAsync<T>(this ISenderContext self, PID target, object message,
        CancellationToken cancellationToken)
    {
        using var future = self.GetFuture();

        var messageEnvelope = message is MessageEnvelope envelope
            ? envelope.WithSender(future.Pid)
            : new MessageEnvelope(message, future.Pid);

        self.Send(target, messageEnvelope);
        var result = await future.GetTask(cancellationToken).ConfigureAwait(false);

        var messageResult = MessageEnvelope.UnwrapMessage(result);

        switch (messageResult)
        {
            case DeadLetterResponse:
                if (self.System.Config.DeadLetterResponseLogging)
                {
                    Logger.LogError("Context {Self} got DeadLetterResponse for PID {Pid}", self.Self, target);
                }

                throw new DeadLetterException(target);
            case null:
            case T:
                return (T)messageResult!;
            default:
                if (typeof(T) == typeof(MessageEnvelope))
                {
                    return (T)(object)MessageEnvelope.Wrap(result);
                }

                throw new InvalidOperationException(
                    $"Unexpected message. Was type {messageResult.GetType()} but expected {typeof(T)}"
                );
        }
    }
}