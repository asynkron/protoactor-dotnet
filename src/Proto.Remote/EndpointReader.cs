// -----------------------------------------------------------------------
//  <copyright file="EndpointReader.cs" company="Asynkron HB">
//      Copyright (C) 2015-2017 Asynkron HB All rights reserved
//  </copyright>
// -----------------------------------------------------------------------

using System.Threading.Tasks;
using Grpc.Core;
using Grpc.Core.Utils;
using System.Collections.Generic;
using Proto.Mailbox;

namespace Proto.Remote
{
    public class EndpointReader : Remoting.RemotingBase
    {
        public override async Task Receive(IAsyncStreamReader<MessageBatch> requestStream,
            IServerStreamWriter<Unit> responseStream, ServerCallContext context)
        {
            await requestStream.ForEachAsync(batch =>
            {
                var targetNames = new List<string>(batch.TargetNames);
                var typeNames = new List<string>(batch.TypeNames);
                foreach (var envelope in batch.Envelopes)
                {
                    
                    var targetName = targetNames[envelope.Target];
                    var target = new PID(ProcessRegistry.Instance.Address, targetName);
                    var sender = envelope.Sender;
                    var typeName = typeNames[envelope.TypeId];
                    var message = Serialization.Deserialize(typeName, envelope.MessageData);

                    if (message is Terminated msg)
                    {
                        var rt = new RemoteTerminate(target, msg.Who);
                        Remote.EndpointManagerPid.Tell(rt);
                    }
                    if (message is SystemMessage sys)
                    {
                        target.SendSystemMessage(sys);
                    }
                    else
                    {
                        target.Request(message, sender);
                    }
                }

                return Actor.Done;
            });
        }
    }
}