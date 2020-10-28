using System;
using Microsoft.Extensions.Logging;

namespace Proto.Cluster
{
    using Mailbox;
    using Proto.Utils;

    public static class LocalAffinityPropsExtensions
    {
        private static readonly ILogger Logger = Log.CreateLogger(nameof(LocalAffinityPropsExtensions));

        private const float DefaultRelocationFactor = 0.2f;

        /// <summary>
        /// Adds middleware which causes remote traffic against the actor to potentially poison it.
        /// Useful with local affinity strategy to move partitioned workloads to the right node, even after a re-balance
        /// </summary>
        /// <param name="props"></param>
        /// <param name="relocationFactor">Chance the actor is poisoned on remote traffic, 0-1</param>
        /// <param name="throttle">Throttling max relocations per timespan</param>
        /// <param name="hasLocalAffinity">Predicate on message envelope, to have local affinity only on partitioned messages</param>
        /// <returns></returns>
        /// <exception cref="ArgumentException"></exception>
        public static Props WithPoisonOnRemoteTraffic(this Props props,
            float relocationFactor = DefaultRelocationFactor,
            ShouldThrottle? throttle = null,
            Predicate<MessageEnvelope>? hasLocalAffinity = null)
        {
            if (relocationFactor <= 0)
            {
                throw new ArgumentException("RelocationFactor must be positive");
            }

            hasLocalAffinity ??= _ => true;
            var shouldRelocate = CreateShouldRelocate(relocationFactor);

            return props.WithReceiverMiddleware(receiver => (context, envelope) =>
                {
                    //Sender is removed from context after call
                    var sender = context.Sender;

                    var task = receiver(context, envelope);

                    if (!(envelope.Message is PoisonPill && !(envelope.Message is SystemMessage))
                        && sender.IsRemote(context)
                        && hasLocalAffinity(envelope)
                        && shouldRelocate()
                        && throttle?.Invoke() != Throttle.Valve.Closed
                        )
                    {
                        Logger.LogDebug("Poisoning {ActorPid}, because of {MessageType} from {Sender}", context.Self,
                            envelope.Message.GetType(), sender
                        );
                        // ReSharper disable once MethodHasAsyncOverload
                        context.System.Root.Send(context.Self!, PoisonPill.Instance);
                    }

                    return task;
                }
            );
        }

        private static Func<bool> CreateShouldRelocate(float relocationFactor)
        {
            if (relocationFactor >= 1)
            {
                return () => true;
            }

            var random = new Random();
            return () => random.NextDouble() < relocationFactor;
        }

        private static bool IsRemote(this PID? sender, IInfoContext context)
            => sender != null && sender.Address != context.System.Address;
    }
}