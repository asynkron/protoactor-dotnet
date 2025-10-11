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
        if (self is null)
        {
            throw new ArgumentNullException(nameof(self));
        }

        var parentProp = typeof(DescriptorProto).GetProperty("Parent", BindingFlags.NonPublic | BindingFlags.Instance);
        if (parentProp is null)
        {
            throw new InvalidOperationException("Parent property not found");
        }

        var parent = parentProp.GetValue(self) as FileDescriptorProto;
        if (parent is null)
        {
            throw new InvalidOperationException("Parent FileDescriptorProto not found");
        }

        return parent;
    }
}