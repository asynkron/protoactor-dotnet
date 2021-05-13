// -----------------------------------------------------------------------
// <copyright file="Proto.cs" company="Asynkron AB">
//      Copyright (C) 2015-2020 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------
using Google.Protobuf.Reflection;

namespace Proto.Cluster.CodeGen.model
{
    public class ProtoMethod
    {
        public int Index { get; set; }
        public string Name { get; set; } = null!;
        public string InputNameRaw { get; set; } = null!;
        public string OutputNameRaw { get; set; } = null!;

        public DescriptorProto InputObject { get; set; } = null!;

        public string InputName => InputObject.File().Options.CsharpNamespace + "." + InputNameRaw;
        
        public DescriptorProto OutputObject { get; set; } = null!;
        
        public string OutputName => OutputObject.File().Options.CsharpNamespace + "." + OutputNameRaw;
    }
}