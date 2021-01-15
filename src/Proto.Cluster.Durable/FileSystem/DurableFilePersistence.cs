// -----------------------------------------------------------------------
// <copyright file="DurableMongoPersistence.cs" company="Asynkron AB">
//      Copyright (C) 2015-2021 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Google.Protobuf;
using Proto.Remote;

namespace Proto.Cluster.Durable.FileSystem
{
    public class DurableFilePersistence
        : IDurablePersistence
    {
        private Cluster _cluster;

        public DurableFilePersistence()
        {
            
        }

        public async Task StartAsync(Cluster cluster)
        {
            _cluster = cluster;
        }

        public async Task<object> GetStartedFunctions()
        {
            var files1 = Directory.GetFiles(".", "*.dur1").OrderBy(f => f);
            var files2 = Directory.GetFiles(".", "*.dur2").OrderBy(f => f);

            foreach (var f in files2)
            {
                
            }
            
            foreach (var f in files1)
            {
                
            }

            return null;
        }
        
        public  async Task PersistRequestAsync(DurableRequest request, object responseMessage)
        {
            if (responseMessage is not IMessage)
            {
                throw new NotSupportedException("Message must be Protobuf message");
            }

            var ser = _cluster.System.Serialization();
            
            var file = $"{request.Id}-{request.Sender.Identity}-{request.Sender.Kind}-{request.Target.Identity}-{request.Target.Kind}.dur2";
            var persistedRequest = new PersistedRequest
            {
                RequestId = request.Id,
                TargetIdentity = request.Target.Identity,
                TargetKind = request.Target.Kind,
                SenderIdentity = request.Sender.Identity,
                SenderKind = request.Sender.Kind,
                MessageType = ser.GetTypeName(responseMessage, ser.DefaultSerializerId),
                MessageData = ser.Serialize(responseMessage, ser.DefaultSerializerId),
            };
            await File.WriteAllBytesAsync(file, persistedRequest.ToByteArray());
        }
        
        public async Task PersistFunctionStartAsync(ClusterIdentity identity, object requestMessage)
        {
            if (requestMessage is not IMessage)
            {
                throw new NotSupportedException("Message must be Protobuf message");
            }
            
            var ser = _cluster.System.Serialization();
            
            var file = $"{identity.Identity}-{identity.Kind}.dur1";
            var persistedRequest = new PersistedFunction()
            {
              Identity = identity.Identity,
              Kind = identity.Kind,
                MessageType = ser.GetTypeName(requestMessage, ser.DefaultSerializerId),
                MessageData = ser.Serialize(requestMessage, ser.DefaultSerializerId),
            };
            
            await File.WriteAllBytesAsync(file, persistedRequest.ToByteArray());           
        }
    }
}