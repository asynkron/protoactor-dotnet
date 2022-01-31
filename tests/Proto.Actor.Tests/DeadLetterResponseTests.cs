using System.Threading.Tasks;
using FluentAssertions;
using Xunit;

namespace Proto.Tests
{
    public class DeadLetterResponseTests
    {
        private static readonly Props EchoProps = Props.FromFunc(context => {
                if (context.Message is string s) context.Respond(s);

                return Task.CompletedTask;
            }
        );

        [Fact]
        public async Task ThrowsDeadLetterException()
        {
            await using var system = new ActorSystem();
            var context = system.Root;

            var echoPid = system.Root.Spawn(EchoProps);

            const string message = "hello";
            var response = await context.RequestAsync<string>(echoPid, message);
            response.Should().Be(message);
            await context.PoisonAsync(echoPid);

            await context.Invoking(c => c.RequestAsync<string>(echoPid, message)).Should()
                .ThrowExactlyAsync<DeadLetterException>();
        }

        [Fact]
        public async Task SendsDeadLetterResponse()
        {
            await using var system = new ActorSystem();
            var context = system.Root;

            var validationActor = Props.FromProducer(() => new DeadLetterResponseValidationActor());

            var pid = context.Spawn(validationActor);

            var response = await context.RequestAsync<string>(pid, "Validate");

            response.Should().Be("Validated");
        }

        private class DeadLetterResponseValidationActor : IActor
        {
            private PID? _deadLetterTarget;
            private PID? _sender;

            public async Task ReceiveAsync(IContext context)
            {
                switch (context.Message)
                {
                    case "Validate":
                        _sender = context.Sender;
                        _deadLetterTarget = context.Spawn(EchoProps);
                        await context.PoisonAsync(_deadLetterTarget);
                        context.Request(_deadLetterTarget, "One dead letter please");
                        break;
                    case DeadLetterResponse response: {
                        response.Target.Should().Be(_deadLetterTarget);
                        context.Send(_sender!, "Validated");
                        break;
                    }
                }
            }
        }
    }
}