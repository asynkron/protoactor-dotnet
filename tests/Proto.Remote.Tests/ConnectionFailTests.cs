using System;
using System.Threading;
using System.Threading.Tasks;
using Divergic.Logging.Xunit;
using Microsoft.Extensions.Logging;
using Proto.Remote.Tests.Messages;
using Xunit;
using Xunit.Abstractions;

namespace Proto.Remote.Tests
{
    [Collection("RemoteTests"), Trait("Category", "Remote")]
    public class ConnectionFailTests
    {
        public ConnectionFailTests(ITestOutputHelper testOutputHelper)
        {
            var factory = LogFactory.Create(testOutputHelper);
            Log.SetLoggerFactory(factory);
        }

        [Fact, DisplayTestMethodName]
        public async Task CanRecoverFromConnectionFailureAsync()
        {
            var logger = Log.CreateLogger("ConnectionFail");

            var remoteActor = new PID("127.0.0.1:12000", "EchoActorInstance");
            var ct = new CancellationTokenSource(30000);
            var tcs = new TaskCompletionSource<bool>();
            var receivedTerminationTcs = new TaskCompletionSource<bool>();

            ct.Token.Register(
                () =>
                {
                    tcs.TrySetCanceled();
                    receivedTerminationTcs.TrySetCanceled();
                }
            );

            EventStream.Instance.Subscribe<EndpointTerminatedEvent>(termEvent => receivedTerminationTcs.TrySetResult(true));

            var localActor = RootContext.Empty.Spawn(
                Props.FromFunc(
                    ctx =>
                    {
                        if (ctx.Message is Pong)
                        {
                            tcs.SetResult(true);
                            ctx.Stop(ctx.Self);
                        }

                        return Actor.Done;
                    }
                )
            );

            var json = new JsonMessage("remote_test_messages.Ping", "{ \"message\":\"Hello\"}");
            var envelope = new Proto.MessageEnvelope(json, localActor, Proto.MessageHeader.Empty);
            Remote.SendMessage(remoteActor, envelope, 1);

            logger.LogDebug("sent message");
            logger.LogDebug("awaiting completion");

            await tcs.Task;
            await receivedTerminationTcs.Task;
        }

        [Fact, DisplayTestMethodName]
        public async Task MessagesGoToDeadLetterAfterConnectionFail()
        {
            var logger = Log.CreateLogger("ConnectionFail");

            var remoteActor = new PID("127.0.0.1:12000", "EchoActorInstance");
            var ct = new CancellationTokenSource(30000);
            var receivedPong = new TaskCompletionSource<bool>();
            var receivedDeadLetterEventTcs = new TaskCompletionSource<bool>();

            ct.Token.Register(
                () =>
                {
                    receivedPong.TrySetCanceled();
                    receivedDeadLetterEventTcs.TrySetCanceled();
                }
            );

            EventStream.Instance.Subscribe<DeadLetterEvent>(
                deadLetterEvt =>
                {
                    if (deadLetterEvt.Message is JsonMessage)
                    {
                        receivedDeadLetterEventTcs.TrySetResult(true);
                    }
                }
            );

            var localActor = RootContext.Empty.Spawn(
                Props.FromFunc(
                    ctx =>
                    {
                        if (ctx.Message is Pong)
                        {
                            receivedPong.SetResult(true);
                            ctx.Stop(ctx.Self);
                        }

                        return Actor.Done;
                    }
                )
            );

            var json = new JsonMessage("remote_test_messages.Ping", "{ \"message\":\"Hello\"}");
            var envelope = new Proto.MessageEnvelope(json, localActor, Proto.MessageHeader.Empty);
            logger.LogDebug("sending message while offline");
            Remote.SendMessage(remoteActor, envelope, 1);

            logger.LogDebug("waiting for connection to fail after retries and fire a terminated event");
            await receivedDeadLetterEventTcs.Task;

            //Should reconnect if we send a new message
            await Task.Delay(2000, ct.Token);
            
            logger.LogDebug("Sending new message now that remote is up");
            Remote.SendMessage(remoteActor, envelope, 1);
            
            logger.LogDebug("Waiting for response to message");
            await receivedPong.Task;
        }
    }
}