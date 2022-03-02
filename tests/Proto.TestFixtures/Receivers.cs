// -----------------------------------------------------------------------
//  <copyright file="ActorFixture.cs" company="Asynkron AB">
//      Copyright (C) 2015-2022 Asynkron AB All rights reserved
//  </copyright>
// -----------------------------------------------------------------------

using System.Threading.Tasks;

namespace Proto.TestFixtures;

public static class Receivers
{
    public static readonly Receive EmptyReceive = c => Task.CompletedTask;
}