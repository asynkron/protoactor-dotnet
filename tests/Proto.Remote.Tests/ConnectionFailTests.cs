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
    public class ConnectionFailTests
    {
        public ConnectionFailTests(ITestOutputHelper testOutputHelper){
            var factory = LogFactory.Create(testOutputHelper);
            Log.SetLoggerFactory(factory);

            

        }
        //[Fact, DisplayTestMethodName]
        public async Task CanRecoverFromConnectionFailureAsync()
        {

            var logger = Log.CreateLogger("ConnectionFail");
            
            var remoteActor = new PID("127.0.0.1:12000", "EchoActorInstance");
            var ct = new CancellationTokenSource(30000);
            var tcs = new TaskCompletionSource<bool>();
            var receivedTerminationTCS = new TaskCompletionSource<bool>();
            ct.Token.Register(() =>
            {
                tcs.TrySetCanceled();
                receivedTerminationTCS.TrySetCanceled();
            });
            var endpointTermEvnSub = EventStream.Instance.Subscribe<EndpointTerminatedEvent>(termEvent => {
                receivedTerminationTCS.TrySetResult(true);
            });

            Remote.Start("127.0.0.1", 12001);

            var localActor = RootContext.Empty.Spawn(Props.FromFunc(ctx =>
            {
                
                if (ctx.Message is Pong)
                {
                    tcs.SetResult(true);
                    ctx.Stop(ctx.Self);
                }

                return Actor.Done;
            }));
            
            var json = new JsonMessage("remote_test_messages.Ping", "{ \"message\":\"Hello\"}");
            var envelope = new Proto.MessageEnvelope(json, localActor, Proto.MessageHeader.Empty);
            Remote.SendMessage(remoteActor, envelope, 1);
            logger.LogDebug("sent message");
            // await Task.Delay(3000);
            logger.LogDebug("starting remote manager");
            using(var remoteService = new RemoteManager(false)){
                logger.LogDebug("awaiting completion");
                await tcs.Task;
            }
            
            Remote.Shutdown(true);
            await receivedTerminationTCS.Task;
        }


       

        //[Fact, DisplayTestMethodName]
        public async Task MessagesGoToDeadLetterAfterConnectionFail()
        {

            var logger = Log.CreateLogger("ConnectionFail");
            
            var remoteActor = new PID("127.0.0.1:12000", "EchoActorInstance");
            var ct = new CancellationTokenSource(30000);
            var receivedPong = new TaskCompletionSource<bool>();
            var receivedDeadLetterEventTCS = new TaskCompletionSource<bool>();
            ct.Token.Register(() =>
            {
                receivedPong.TrySetCanceled();
                receivedDeadLetterEventTCS.TrySetCanceled();
            });
            var deadLetterEvnSub = EventStream.Instance.Subscribe<DeadLetterEvent>(deadLetterEvt => {
                if(deadLetterEvt.Message is JsonMessage){
                    receivedDeadLetterEventTCS.TrySetResult(true);
                }
                
            });
            var config = new RemoteConfig{
                EndpointWriterOptions = new EndpointWriterOptions {
                    MaxRetries = 2,
                    RetryBackOffms = 10,
                    RetryTimeSpan = TimeSpan.FromSeconds(120)
                }
            };

            Remote.Start("127.0.0.1", 12001, config);

            var localActor = RootContext.Empty.Spawn(Props.FromFunc(ctx =>
            {
                
                if (ctx.Message is Pong)
                {
                    receivedPong.SetResult(true);
                    ctx.Stop(ctx.Self);
                }

                return Actor.Done;
            }));
            
            var json = new JsonMessage("remote_test_messages.Ping", "{ \"message\":\"Hello\"}");
            var envelope = new Proto.MessageEnvelope(json, localActor, Proto.MessageHeader.Empty);
            logger.LogDebug("sending messge while offline");
            Remote.SendMessage(remoteActor, envelope, 1);
            
            
            logger.LogDebug("waiting for connection to fail after retries and fire a terminated event");
            await receivedDeadLetterEventTCS.Task;
            
            logger.LogDebug("Starting Remote service");
            //Should reconnect if we send a new message
            using(var remoteService = new RemoteManager(false)){
                await Task.Delay(2000);
                logger.LogDebug("Sending new message now that remote is up");
                Remote.SendMessage(remoteActor, envelope, 1);
                logger.LogDebug("Waiting for response to message");
                await receivedPong.Task;
            }
            
            Remote.Shutdown(true);
            
        }

        

        
    }
}