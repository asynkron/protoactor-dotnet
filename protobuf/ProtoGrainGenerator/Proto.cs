// -----------------------------------------------------------------------
// <copyright file="Proto.cs" company="Asynkron AB">
//      Copyright (C) 2015-2020 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------
namespace ProtoBuf
{
    public class ProtoFile
    {
        public string PackageName { get; set; } = null!;
        public string CsNamespace { get; set; } = null!;
        public ProtoMessage[] Messages { get; set; } = null!;
        public ProtoService[] Services { get; set; } = null!;
    }

    public class ProtoMessage
    {
        public string Name { get; set; } = null!;
    }

    public class ProtoService
    {
        public string Name { get; set; } = null!;
        public ProtoMethod[] Methods { get; set; } = null!;
    }

    public class ProtoMethod
    {
        public int Index { get; set; }
        public string Name { get; set; } = null!;
        public string InputName { get; set; } = null!;
        public string OutputName { get; set; } = null!;
    }
}