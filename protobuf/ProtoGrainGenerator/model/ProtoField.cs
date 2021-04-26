// -----------------------------------------------------------------------
// <copyright file="ProtoField.cs" company="Asynkron AB">
//      Copyright (C) 2015-2021 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------
using System;
using Google.Protobuf.Reflection;

namespace ProtoBuf
{
    public class ProtoField
    {
        public string TypeName { get; set; } = null!;

        public string Name { get; set; } = null!;

        public int Number { get; set; }

        public bool IsRepeated { get; set; }
        public int OneOfIndex { get; set; }
        public FieldDescriptorProto.Type Type { get; set; }
        public DescriptorProto Object { get; set; } = null!;
        
        public string CsharpTypeName => Type switch
        {
            FieldDescriptorProto.Type.TypeDouble   => nameof(Double),
            FieldDescriptorProto.Type.TypeFloat    => nameof(Single),
            FieldDescriptorProto.Type.TypeInt64    => nameof(Int64),
            FieldDescriptorProto.Type.TypeUint64   => nameof(UInt64),
            FieldDescriptorProto.Type.TypeInt32    => nameof(Int32),
            FieldDescriptorProto.Type.TypeFixed64  => nameof(Int64),
            FieldDescriptorProto.Type.TypeFixed32  => nameof(Int32),
            FieldDescriptorProto.Type.TypeBool     => nameof(Boolean),
            FieldDescriptorProto.Type.TypeString   => nameof(String),
            FieldDescriptorProto.Type.TypeGroup    => "???",
            FieldDescriptorProto.Type.TypeMessage  => GetUserTypeName(),
            FieldDescriptorProto.Type.TypeBytes    => "ByteString",
            FieldDescriptorProto.Type.TypeUint32   => nameof(UInt32),
            FieldDescriptorProto.Type.TypeEnum     => GetUserTypeName(),
            FieldDescriptorProto.Type.TypeSfixed32 => nameof(Int32),
            FieldDescriptorProto.Type.TypeSfixed64 => nameof(Int64),
            FieldDescriptorProto.Type.TypeSint32   => nameof(Int32),
            FieldDescriptorProto.Type.TypeSint64   => nameof(Int64),
            _                                      => throw new ArgumentOutOfRangeException()
        };

        private string GetUserTypeName() => $"{Object.File().Options.CsharpNamespace}.{TypeName}";
    }
}