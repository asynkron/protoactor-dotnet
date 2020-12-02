// -----------------------------------------------------------------------
// <copyright file="Proto.cs" company="Asynkron AB">
//      Copyright (C) 2015-2020 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------
namespace ProtoBuf
{
    public class ProtoFile
    {
        public string PackageName { get; set; }
        public string CsNamespace { get; set; }
        public ProtoMessage[] Messages { get; set; }
        public ProtoService[] Services { get; set; }
    }

    public class ProtoMessage
    {
        public string Name { get; set; }
    }

    public class ProtoService
    {
        public string Name { get; set; }
        public ProtoMethod[] Methods { get; set; }
    }

    public class ProtoMethod
    {
        public int Index { get; set; }
        public string Name { get; set; }
        public string InputName { get; set; }
        public string OutputName { get; set; }
    }
}