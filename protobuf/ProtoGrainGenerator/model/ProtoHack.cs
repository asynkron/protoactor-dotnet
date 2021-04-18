// -----------------------------------------------------------------------
// <copyright file="ProtoHack.cs" company="Asynkron AB">
//      Copyright (C) 2015-2021 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------
using System.Reflection;
using Google.Protobuf.Reflection;

namespace ProtoBuf
{
    public static class ProtoHack
    {
        public static FileDescriptorProto File(this DescriptorProto self)
        {
            var parentProp = typeof(DescriptorProto).GetProperty("Parent", BindingFlags.NonPublic | BindingFlags.Instance);
            var parent = (FileDescriptorProto) parentProp!.GetValue(self);
            return parent;
        }

    }
}