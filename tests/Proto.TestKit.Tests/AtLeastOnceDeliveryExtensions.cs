using System;
using System.Threading.Tasks;

namespace Proto.TestKit.Tests
{
    public class Confirmation { }

    public class FailedDelivery
    {
        public object Message { get; set; }
        public PID Target { get; set; }
        public TimeSpan? TimeOut { get; set; }
        public int MaxAttempts { get; set; }

        public Task Retry(IContext context) => context.AtLeastOnceDelivery(Target, Message, TimeOut, MaxAttempts);
    }

    public static class AtLeastOnceDeliveryExtensions
    {
        public static void SendConfirmation(this IContext context)
        {
            if (context.Sender == null)
                return;

            context.Respond(new Confirmation());
        }

        public static Task AtLeastOnceDelivery(this IContext context, PID target, object message, TimeSpan? timeout = null, int maxAttempts = 10)
            => Task.Run(
                async () =>
                {
                    var attempts = 1;

                    while (true)
                    {
                        try
                        {
                            await context.RequestAsync<Confirmation>(target, message, timeout ?? TimeSpan.FromSeconds(2));
                            return;
                        }
                        catch
                        {
                            attempts++;

                            if (attempts <= maxAttempts)
                                continue;

                            context.Send(
                                context.Self, new FailedDelivery
                                {
                                    Message = message,
                                    Target = target,
                                    TimeOut = timeout,
                                    MaxAttempts = maxAttempts
                                }
                            );
                            return;
                        }
                    }
                }
            );
    }
}