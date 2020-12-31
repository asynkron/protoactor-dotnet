// -----------------------------------------------------------------------
//   <copyright file="EndpointReader.cs" company="Asynkron AB">
//       Copyright (C) 2015-2020 Asynkron AB All rights reserved
//   </copyright>
// -----------------------------------------------------------------------

using System;
using System.Linq;
using System.Threading.Tasks;
using Grpc.Core;
using Microsoft.Extensions.Logging;
using Proto.Mailbox;

namespace Proto.Remote
{
    public class EndpointReader : Remoting.RemotingBase
    {
        private static readonly ILogger Logger = Log.CreateLogger<EndpointReader>();
        private readonly EndpointManager _endpointManager;
        private readonly Serialization _serialization;
        private readonly ActorSystem _system;

        public EndpointReader(ActorSystem system, EndpointManager endpointManager, Serialization serialization)
        {
            _system = system;
            _endpointManager = endpointManager;
            _serialization = serialization;
        }

        public override Task<ConnectResponse> Connect(ConnectRequest request, ServerCallContext context)
        {
            if (_endpointManager.CancellationToken.IsCancellationRequested)
            {
                Logger.LogWarning("[EndpointReader] Attempt to connect to the suspended reader has been rejected");

                throw new RpcException(Status.DefaultCancelled, "Suspended");
            }

            Logger.LogDebug("[EndpointReader] Accepted connection request from {Remote} to {Local}", context.Peer,
                context.Host
            );

            return Task.FromResult(
                new ConnectResponse
                {
                    DefaultSerializerId = _serialization.DefaultSerializerId
                }
            );
        }

        public override async Task Receive(
            IAsyncStreamReader<MessageBatch> requestStream,
            IServerStreamWriter<Unit> responseStream,
            ServerCallContext context
        )
        {
            using var cancellationTokenRegistration = _endpointManager.CancellationToken.Register(() => {
                    Logger.LogDebug("[EndpointReader] Telling to {Address} to stop", context.Peer);

                    try
                    {
                        _ = responseStream.WriteAsync(new Unit());
                    }
                    catch (Exception e)
                    {
                        Logger.LogError(e, "[EndpointReader] Didn't tell to {Address} to stop", context.Peer);
                    }
                }
            );

            var targets = new PID[100];

            while (await requestStream.MoveNext(context.CancellationToken).ConfigureAwait(false))
            {
                if (_endpointManager.CancellationToken.IsCancellationRequested)
                {
                    // We read all the messages ignoring them to gracefully end the request
                    continue;
                }

                var batch = requestStream.Current;

                Logger.LogDebug("[EndpointReader] Received a batch of {Count} messages from {Remote}",
                    batch.TargetNames.Count, context.Peer
                );

                //only grow pid lookup if needed
                if (batch.TargetNames.Count > targets.Length) targets = new PID[batch.TargetNames.Count];

                for (var i = 0; i < batch.TargetNames.Count; i++)
                {
                    targets[i] = PID.FromAddress(_system.Address, batch.TargetNames[i]);
                }

                var typeNames = batch.TypeNames.ToArray();

                foreach (var envelope in batch.Envelopes)
                {
                    var target = targets[envelope.Target];
                    var typeName = typeNames[envelope.TypeId];
                    var message =
                        _serialization.Deserialize(typeName, envelope.MessageData, envelope.SerializerId);

                    switch (message)
                    {
                        case Terminated msg:
                            Terminated(msg, target);
                            break;
                        case SystemMessage sys:
                            SystemMessage(sys, target);
                            break;
                        default:
                            ReceiveMessages(envelope, message, target);
                            break;
                    }
                }
            }

            Logger.LogDebug("[EndpointReader] Stream closed by {Remote}", context.Peer);
        }

        private void ReceiveMessages(MessageEnvelope envelope, object message, PID target)
        {
            Proto.MessageHeader? header = null;

            if (envelope.MessageHeader is not null) header = new Proto.MessageHeader(envelope.MessageHeader.HeaderData);

            // Logger.LogDebug("[EndpointReader] Forwarding remote user message {@Message}", message);
            var localEnvelope = new Proto.MessageEnvelope(message, envelope.Sender, header);
            _system.Root.Send(target, localEnvelope);
        }

        private void SystemMessage(SystemMessage sys, PID target) =>
            // Logger.LogDebug(
            //     "[EndpointReader] Forwarding remote system message {@MessageType}:{@Message}",
            //     sys.GetType().Name, sys
            // );
            target.SendSystemMessage(_system, sys);

        private void Terminated(Terminated msg, PID target)
        {
            // Logger.LogDebug(
            //     "[EndpointReader] Forwarding remote endpoint termination request for {Who}", msg.Who
            // );

            var rt = new RemoteTerminate(target, msg.Who);
            var endpoint = _endpointManager.GetEndpoint(rt.Watchee.Address);
            if (endpoint is null) return;

            _system.Root.Send(endpoint, rt);
        }
    }
}