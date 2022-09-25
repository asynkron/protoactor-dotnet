// using System;
// using System.Threading;
// using System.Threading.Tasks;
// using Divergic.Logging.Xunit;
// using Microsoft.Extensions.Logging;
// using Proto.Remote.GrpcNet;
// using Proto.Remote.Tests.Messages;
// using Xunit;
// using Xunit.Abstractions;
//
// namespace Proto.Remote.Tests
// {
//     [Collection("RemoteTests"), Trait("Category", "Remote")]
//     public class ConnectionFailTests
//     {
//         public ConnectionFailTests(ITestOutputHelper testOutputHelper)
//         {
//             var factory = LogFactory.Create(testOutputHelper);
//             Log.SetLoggerFactory(factory);
//         }
//
//         [Fact, DisplayTestMethodName]
//         public async Task CanRecoverFromConnectionFailureAsync()
//         {
//             var system = new ActorSystem().WithRemote(GrpcNetRemoteConfig.BindToLocalhost().WithProtoMessages(Tests.Messages.ProtosReflection.Descriptor));
//             await system.Remote().StartAsync();
//             
//             var logger = Log.CreateLogger("ConnectionFail");
//
//             var remoteActor = new PID("127.0.0.1:12000", "EchoActorInstance");
//             var ct = new CancellationTokenSource(30000);
//             var tcs = new TaskCompletionSource<bool>();
//             var receivedTerminationTcs = new TaskCompletionSource<bool>();
//
//             ct.Token.Register(
//                 () =>
//                 {
//                     tcs.TrySetCanceled();
//                     receivedTerminationTcs.TrySetCanceled();
//                 }
//             );
//
//             system.EventStream.Subscribe<EndpointTerminatedEvent>(termEvent => receivedTerminationTcs.TrySetResult(true));
//
//             var localActor = system.Root.Spawn(
//                 Props.FromFunc(
//                     ctx =>
//                     {
//                         if (ctx.Message is Pong)
//                         {
//                             tcs.SetResult(true);
//                             ctx.Stop(ctx.Self);
//                         }
//
//                         return Task.CompletedTask;
//                     }
//                 )
//             );
//
//             var ping = new Ping() {Message = "hello"};
//             var envelope = new Proto.MessageEnvelope(ping, localActor, Proto.MessageHeader.Empty);
//             system.Root.Send(remoteActor, envelope);
//
//             logger.LogDebug("sent message");
//             logger.LogDebug("awaiting completion");
//
//             await tcs.Task;
//             await receivedTerminationTcs.Task;
//         }
//
//         [Fact, DisplayTestMethodName]
//         public async Task MessagesGoToDeadLetterAfterConnectionFail()
//         {
//             var system = new ActorSystem().WithRemote(GrpcNetRemoteConfig.BindToLocalhost());
//             await system.Remote().StartAsync();
//             var logger = Log.CreateLogger("ConnectionFail");
//
//             var remoteActor = new PID("127.0.0.1:12000", "EchoActorInstance");
//             var ct = new CancellationTokenSource(30000);
//             var receivedPong = new TaskCompletionSource<bool>();
//             var receivedDeadLetterEventTcs = new TaskCompletionSource<bool>();
//
//             ct.Token.Register(
//                 () =>
//                 {
//                     receivedPong.TrySetCanceled();
//                     receivedDeadLetterEventTcs.TrySetCanceled();
//                 }
//             );
//
//             system.EventStream.Subscribe<DeadLetterEvent>(
//                 deadLetterEvt =>
//                 {
//                     if (deadLetterEvt.Message is Ping)
//                     {
//                         receivedDeadLetterEventTcs.TrySetResult(true);
//                     }
//                 }
//             );
//
//             var localActor = system.Root.Spawn(
//                 Props.FromFunc(
//                     ctx =>
//                     {
//                         if (ctx.Message is Pong)
//                         {
//                             receivedPong.SetResult(true);
//                             ctx.Stop(ctx.Self);
//                         }
//
//                         return Task.CompletedTask;
//                     }
//                 )
//             );
//
//             var ping = new Ping() {Message = "hello"};
//             var envelope = new Proto.MessageEnvelope(ping, localActor, Proto.MessageHeader.Empty);
//             logger.LogDebug("sending message while offline");
//             system.Root.Send(remoteActor, envelope);
//
//             logger.LogDebug("waiting for connection to fail after retries and fire a terminated event");
//             await receivedDeadLetterEventTcs.Task;
//
//             //Should reconnect if we send a new message
//             await Task.Delay(2000, ct.Token);
//             
//             logger.LogDebug("Sending new message now that remote is up");
//             system.Root.Send(remoteActor, envelope);
//             
//             logger.LogDebug("Waiting for response to message");
//             await receivedPong.Task;
//         }
//     }
// }

