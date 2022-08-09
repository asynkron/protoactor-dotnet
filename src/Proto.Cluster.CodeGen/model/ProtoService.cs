// -----------------------------------------------------------------------
// <copyright file="ProtoService.cs" company="Asynkron AB">
//      Copyright (C) 2015-2022 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------
namespace Proto.Cluster.CodeGen.model;

public class ProtoService
{
    public string Name { get; set; } = null!;
    public string Kind { get; set; } = null!;
    public ProtoMethod[] Methods { get; set; } = null!;
}