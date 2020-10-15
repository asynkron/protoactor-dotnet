// -----------------------------------------------------------------------
//   <copyright file="EndpointReader.cs" company="Asynkron AB">
//       Copyright (C) 2015-2020 Asynkron AB All rights reserved
//   </copyright>
// -----------------------------------------------------------------------

using System;
using System.Linq;
using System.Threading.Tasks;
using Grpc.Core;
using Grpc.Core.Utils;
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

        private bool _suspended;

        public EndpointReader(ActorSystem system, EndpointManager endpointManager, Serialization serialization)
        {
            _system = system;
            _endpointManager = endpointManager;
            _serialization = serialization;
        }

        public override Task<ConnectResponse> Connect(ConnectRequest request, ServerCallContext context)
        {
            if (_suspended)
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
                    DefaultSerializerId = Serialization.DefaultSerializerId
                }
            );
        }

        public override Task Receive(
            IAsyncStreamReader<MessageBatch> requestStream,
            IServerStreamWriter<Unit> responseStream, ServerCallContext context
        )
        {
            var targets = new PID[100];

            return requestStream.ForEachAsync(
                batch =>
                {
                    Logger.LogDebug("[EndpointReader] Received a batch of {Count} messages from {Remote}",
                        batch.TargetNames.Count, context.Peer
                    );

                    if (_suspended)
                    {
                        return Task.CompletedTask;
                    }

                    //only grow pid lookup if needed
                    if (batch.TargetNames.Count > targets.Length)
                    {
                        targets = new PID[batch.TargetNames.Count];
                    }

                    for (var i = 0; i < batch.TargetNames.Count; i++)
                    {
                        targets[i] = new PID(_system.Address, batch.TargetNames[i]);
                    }

                    var typeNames = batch.TypeNames.ToArray();

                    foreach (var envelope in batch.Envelopes)
                    {
                        var target = targets[envelope.Target];
                        var typeName = typeNames[envelope.TypeId];
                        try
                        {
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
                        catch (Exception e)
                        {
                            Logger.LogError(e, "Failed to receive {Type} from {Remote}", typeName, context.Peer);
                        }
                    }

                    return Task.CompletedTask;
                }
            );
        }

        private void ReceiveMessages(MessageEnvelope envelope, object message, PID target)
        {
            Proto.MessageHeader? header = null;

            if (envelope.MessageHeader != null)
            {
                header = new Proto.MessageHeader(envelope.MessageHeader.HeaderData);
            }

            Logger.LogDebug("[EndpointReader] Forwarding remote user message {@Message}", message);
            var localEnvelope = new Proto.MessageEnvelope(message, envelope.Sender, header);
            _system.Root.Send(target, localEnvelope);
        }

        private void SystemMessage(SystemMessage sys, PID target)
        {
            Logger.LogDebug(
                "[EndpointReader] Forwarding remote system message {@MessageType}:{@Message}",
                sys.GetType().Name, sys
            );

            target.SendSystemMessage(_system, sys);
        }

        private void Terminated(Terminated msg, PID target)
        {
            Logger.LogDebug(
                "[EndpointReader] Forwarding remote endpoint termination request for {Who}", msg.Who
            );

            var rt = new RemoteTerminate(target, msg.Who);
            _endpointManager.RemoteTerminate(rt);
        }

        public void Suspend(bool suspended)
        {
            Logger.LogDebug("[EndpointReader] Suspended");
            _suspended = suspended;
        }
    }
}