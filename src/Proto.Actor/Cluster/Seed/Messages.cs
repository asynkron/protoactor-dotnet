// -----------------------------------------------------------------------
// <copyright file = "Messages.cs" company = "Asynkron AB">
//      Copyright (C) 2015-2022 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------

namespace Proto.Cluster.Seed;

public record Connect;

public record Connected(Member Member);

public record FailedToConnect;