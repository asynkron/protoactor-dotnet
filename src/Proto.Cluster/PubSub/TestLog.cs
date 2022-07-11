// -----------------------------------------------------------------------
// <copyright file = "TestLog.cs" company = "Asynkron AB">
//      Copyright (C) 2015-2022 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------
using System;

namespace Proto.Cluster.PubSub;

public static class TestLog
{
    public static Action<string>? Log { get; set; }
}