// -----------------------------------------------------------------------
// <copyright file="TestKitFixture.cs" company="Asynkron AB">
//      Copyright (C) 2015-2025 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------

using System;
using Proto.TestKit;

namespace ActorTestKit;

// xUnit fixture that sets up and tears down the TestKit probe and system.
public class TestKitFixture : TestKitBase, IDisposable
{
    public TestKitFixture() => SetUp();

    public void Dispose() => TearDown();
}
