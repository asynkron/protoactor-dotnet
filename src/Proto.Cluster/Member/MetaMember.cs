// -----------------------------------------------------------------------
// <copyright file="MetaMember.cs" company="Asynkron AB">
//      Copyright (C) 2015-2022 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------

namespace Proto.Cluster;

public record MetaMember(Member Member, int Index);