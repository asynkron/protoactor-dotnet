// -----------------------------------------------------------------------
// <copyright file="Extensions.cs" company="Asynkron AB">
//      Copyright (C) 2015-2022 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------

using System;
using Google.Protobuf;
using Proto.Diagnostics;
using Proto.Remote;

namespace Proto.Cluster;

/// <summary>
///     A request message wrapper used for code-generated virtual actors (grains).
/// </summary>
/// <param name="MethodIndex">Index of the code generated method that should process the request message</param>
/// <param name="RequestMessage">Wrapped message</param>
public record GrainRequestMessage(int MethodIndex, IMessage? RequestMessage) : IRootSerializable, IDiagnosticsTypeName
{
    //serialize into the on-the-wire format
    public IRootSerialized Serialize(ActorSystem system)
    {
        if (RequestMessage is null)
        {
            return new GrainRequest { MethodIndex = MethodIndex };
        }

        var ser = system.Serialization();
        var (data, typeName, serializerId) = ser.Serialize(RequestMessage);
#if DEBUG
        if (serializerId != Serialization.SERIALIZER_ID_PROTOBUF)
        {
            throw new Exception($"Grains must use ProtoBuf types: {RequestMessage.GetType().FullName}");
        }
#endif

        return new GrainRequest
        {
            MethodIndex = MethodIndex,
            MessageData = data,
            MessageTypeName = typeName
        };
    }

    public string GetTypeName()
    {
        var m = RequestMessage?.GetType().Name ?? "null";
        return m;
    }
}