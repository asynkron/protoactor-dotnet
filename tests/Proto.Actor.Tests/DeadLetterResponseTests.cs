namespace Proto.Tests
{
    using System.Threading.Tasks;
    using FluentAssertions;
    using Xunit;

    public class DeadLetterResponseTests
    {
        private static readonly ActorSystem System = new ActorSystem();
        private static readonly RootContext Context = System.Root;


        private static readonly Props EchoProps = Props.FromFunc(context =>
            {
                if (context.Message is string s) context.Respond(s);

                return Task.CompletedTask;
            }
        );

        [Fact]
        public async Task ThrowsDeadLetterException()
        {
            var echoPid = System.Root.Spawn(EchoProps);

            const string message = "hello";
            var response = await Context.RequestAsync<string>(echoPid, message);
            response.Should().Be(message);
            await Context.PoisonAsync(echoPid);

            Context.Invoking(context => context.RequestAsync<string>(echoPid, message)).Should()
                .ThrowExactly<DeadLetterException>();
        }

        [Fact]
        public async Task SendsDeadLetterResponse()
        {
            var validationActor = Props.FromProducer(() => new DeadLetterResponseValidationActor());

            var pid = Context.Spawn(validationActor);

            var response = await Context.RequestAsync<string>(pid, "Validate");

            response.Should().Be("Validated");
        }

        private class DeadLetterResponseValidationActor : IActor
        {
            private PID _sender;
            private PID _deadLetterTarget;

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
                    case DeadLetterResponse response:
                    {
                        response.Target.Should().Be(_deadLetterTarget);
                        context.Send(_sender, "Validated");
                        break;
                    }
                }
            }
        }
    }
}