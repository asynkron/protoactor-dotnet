// -----------------------------------------------------------------------
// <copyright file="ProtoService.cs" company="Asynkron AB">
//      Copyright (C) 2015-2021 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------
namespace ProtoBuf
{
    public class ProtoService
    {
        public string Name { get; set; } = null!;
        public ProtoMethod[] Methods { get; set; } = null!;
    }
}