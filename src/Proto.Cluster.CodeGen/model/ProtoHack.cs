// -----------------------------------------------------------------------
// <copyright file="ProtoHack.cs" company="Asynkron AB">
//      Copyright (C) 2015-2025 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------

using System;
using System.Reflection;
using Google.Protobuf.Reflection;

namespace Proto.Cluster.CodeGen.model;

public static class ProtoHack
{
    public static FileDescriptorProto File(this DescriptorProto self)
    {
        ArgumentNullException.ThrowIfNull(self);

        var parentProp = typeof(DescriptorProto).GetProperty("Parent", BindingFlags.NonPublic | BindingFlags.Instance);
        var parent = (FileDescriptorProto)parentProp!.GetValue(self);

        return parent;
    }
}