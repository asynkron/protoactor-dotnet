// -----------------------------------------------------------------------
// <copyright file="ProtoFile.cs" company="Asynkron AB">
//      Copyright (C) 2015-2021 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------
namespace Proto.Cluster.CodeGen.model
{
    public class ProtoFile
    {
        public string Filename { get; set; } = null!;
        public string PackageName { get; set; } = null!;
        public string CsNamespace { get; set; } = null!;
        public ProtoMessage[] Messages { get; set; } = null!;
        public ProtoService[] Services { get; set; } = null!;
    }
}