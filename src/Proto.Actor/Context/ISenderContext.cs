// -----------------------------------------------------------------------
// <copyright file="ISenderContext.cs" company="Asynkron AB">
//      Copyright (C) 2015-2020 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------
using System;
using System.Threading;
using System.Threading.Tasks;
using Proto.Future;

// ReSharper disable once CheckNamespace
namespace Proto
{
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
    }

    public static class SenderContextExtensions
    {
        /// <summary>
        ///     Sends a message together with a Sender PID, this allows the target to respond async to the Sender
        /// </summary>
        /// <param name="target">The target PID</param>
        /// <param name="message">The message to send</param>
        public static void Request(this ISenderContext self, PID target, object message)
        {
            var messageEnvelope = new MessageEnvelope(message, self.Self);
            self.Send(target, messageEnvelope);
        }

        /// <summary>
        ///     Sends a message together with a Sender PID, this allows the target to respond async to the Sender
        /// </summary>
        /// <param name="target">The target PID</param>
        /// <param name="message">The message to send</param>
        /// <param name="sender">Message sender</param>
        public static void Request(this ISenderContext self, PID target, object message, PID? sender)
        {
            var messageEnvelope = new MessageEnvelope(message, sender);
            self.Send(target, messageEnvelope);
        }

        /// <summary>
        ///     Sends a message together with a Sender PID, this allows the target to respond async to the Sender.
        ///     This operation can be awaited.
        /// </summary>
        /// <param name="target">The target PID</param>
        /// <param name="message">The message to send</param>
        /// <param name="timeout">Timeout for the request</param>
        /// <typeparam name="T">Expected return message type</typeparam>
        /// <returns>A Task that completes once the Target Responds back to the Sender</returns>
        public static Task<T> RequestAsync<T>(this ISenderContext self, PID target, object message, TimeSpan timeout)
            => self.RequestAsync<T>(target, message, new FutureProcess(self.System, timeout));

        /// <summary>
        ///     Sends a message together with a Sender PID, this allows the target to respond async to the Sender.
        ///     This operation can be awaited.
        /// </summary>
        /// <param name="target">The target PID</param>
        /// <param name="message">The message to send</param>
        /// <param name="cancellationToken">Cancellation token for the request</param>
        /// <typeparam name="T">Expected return message type</typeparam>
        /// <returns>A Task that completes once the Target Responds back to the Sender</returns>
        public static Task<T> RequestAsync<T>(this ISenderContext self, PID target, object message, CancellationToken cancellationToken)
            => self.RequestAsync<T>(target, message, new FutureProcess(self.System, cancellationToken));

        /// <summary>
        ///     Sends a message together with a Sender PID, this allows the target to respond async to the Sender.
        ///     This operation can be awaited.
        /// </summary>
        /// <param name="target">The target PID</param>
        /// <param name="message">The message to send</param>
        /// <typeparam name="T">Expected return message type</typeparam>
        /// <returns>A Task that completes once the Target Responds back to the Sender</returns>
        public static Task<T> RequestAsync<T>(this ISenderContext self, PID target, object message) =>
            self.RequestAsync<T>(target, message, new FutureProcess(self.System));

        /// <summary>
        /// Poison will tell actor to stop after processing current user messages in mailbox.
        /// </summary>
        public static void Poison(this ISenderContext self, PID pid) => pid.SendUserMessage(self.System, PoisonPill.Instance);

        /// <summary>
        /// PoisonAsync will tell and wait actor to stop after processing current user messages in mailbox.
        /// </summary>
        public static Task PoisonAsync(this ISenderContext self, PID pid)
            => self.RequestAsync<Terminated>(pid, PoisonPill.Instance, CancellationToken.None);


        private static async Task<T> RequestAsync<T>(this ISenderContext self, PID target, object message, FutureProcess future)
        {
            var messageEnvelope = new MessageEnvelope(message, future.Pid);
            self.Send(target, messageEnvelope);
            var result = await future.Task;

            switch (result)
            {
                case DeadLetterResponse:
                    throw new DeadLetterException(target);
                case null:
                case T:
                    return (T) result!;
                default:
                    throw new InvalidOperationException(
                        $"Unexpected message. Was type {result?.GetType()} but expected {typeof(T)}"
                    );
            }
        }

        /// <summary>
        /// Stop will tell actor to stop immediately, regardless of existing user messages in mailbox.
        /// </summary>
        public static void Stop(this ISenderContext self, PID? pid)
        {
            if (pid is null) return;

            var reff = self.System.ProcessRegistry.Get(pid);
            reff.Stop(pid);
        }

        /// <summary>
        /// StopAsync will tell and wait actor to stop immediately, regardless of existing user messages in mailbox.
        /// </summary>
        public static Task StopAsync(this ISenderContext self, PID pid)
        {
            var future = new FutureProcess(self.System);

            pid.SendSystemMessage(self.System, new Watch(future.Pid));
            self.Stop(pid);

            return future.Task;
        }
    }
}