// -----------------------------------------------------------------------
//  <copyright file="ActorFixture.cs" company="Asynkron AB">
//      Copyright (C) 2015-2020 Asynkron AB All rights reserved
//  </copyright>
// -----------------------------------------------------------------------


namespace Proto.TestFixtures
{
    public static class Receivers
    {
        public static Receive EmptyReceive = c => Actor.Done;
    }
}