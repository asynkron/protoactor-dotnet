// -----------------------------------------------------------------------
// <copyright file="Proto.cs" company="Asynkron AB">
//      Copyright (C) 2015-2020 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------
using System;
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

        public string Parameter => UseParameter ? "request" : string.Empty;
        public string SingleParameterDefinition => UseParameter ? $"{InputName} {Parameter}" : string.Empty;
        public string LeadingParameterDefinition => UseParameter ? $"{InputName} {Parameter}, " : string.Empty;
        public bool EmptyReturn { get; set; }
        public bool EmptyParameter { get; set; }
        public bool UseParameter => !EmptyParameter;
        public bool UseReturn => !EmptyReturn;
    }
}