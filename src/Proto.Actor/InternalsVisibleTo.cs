// -----------------------------------------------------------------------
// <copyright file="InternalsVisibleTo.cs" company="Asynkron AB">
//      Copyright (C) 2015-2022 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------
using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("Proto.Cluster")]
[assembly: InternalsVisibleTo("Proto.Remote")]
[assembly: InternalsVisibleTo("Proto.OpenTelemetry.Tests")]