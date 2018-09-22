// -----------------------------------------------------------------------
//   <copyright file="EndpointReader.cs" company="Asynkron HB">
//       Copyright (C) 2015-2018 Asynkron HB All rights reserved
//   </copyright>
// -----------------------------------------------------------------------

using System.Linq;
using System.Threading.Tasks;
using Grpc.Core;
using Grpc.Core.Utils;
using Proto.Mailbox;

namespace Proto.Remote
{
    public class EndpointReader : Remoting.RemotingBase
    {
        private bool _suspended;

        public override Task<ConnectResponse> Connect(ConnectRequest request, ServerCallContext context)
        {
            if (_suspended)
            {
                throw new RpcException(Status.DefaultCancelled, "Suspended");
            }

            return Task.FromResult(new ConnectResponse()
            {
                DefaultSerializerId = Serialization.DefaultSerializerId
            });
        }

        public override async Task Receive(IAsyncStreamReader<MessageBatch> requestStream,
            IServerStreamWriter<Unit> responseStream, ServerCallContext context)
        {
            var targets = new PID[100];
            await requestStream.ForEachAsync(batch =>
            {
                if (_suspended)
                    return Actor.Done;

                //only grow pid lookup if needed
                if (batch.TargetNames.Count > targets.Length)
                {
                    targets = new PID[batch.TargetNames.Count];
                }

                for (int i = 0; i < batch.TargetNames.Count; i++)
                {
                    targets[i] = new PID(ProcessRegistry.Instance.Address, batch.TargetNames[i]);
                }
                var typeNames = batch.TypeNames.ToArray();
                foreach (var envelope in batch.Envelopes)
                {
                    var target = targets[envelope.Target];
                    var typeName = typeNames[envelope.TypeId];
                    var message = Serialization.Deserialize(typeName, envelope.MessageData, envelope.SerializerId);

                    if (message is Terminated msg)
                    {
                        var rt = new RemoteTerminate(target, msg.Who);
                        EndpointManager.RemoteTerminate(rt);
                    }
                    else if (message is SystemMessage sys)
                    {
                        target.SendSystemMessage(sys);
                    }
                    else
                    {
                        Proto.MessageHeader header = null;
                        if (envelope.MessageHeader != null)
                        {
                            header = new Proto.MessageHeader(envelope.MessageHeader.HeaderData);
                        }
                        var localEnvelope = new Proto.MessageEnvelope(message, envelope.Sender, header);
                        RootContext.Empty.Send(target, localEnvelope);
                    }
                }

                return Actor.Done;
            });
        }

        public void Suspend(bool suspended)
        {
            _suspended = suspended;
        }
    }
}