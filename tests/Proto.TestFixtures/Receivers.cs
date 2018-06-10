// -----------------------------------------------------------------------
//  <copyright file="ActorFixture.cs" company="Asynkron HB">
//      Copyright (C) 2015-2018 Asynkron HB All rights reserved
//  </copyright>
// -----------------------------------------------------------------------


namespace Proto.TestFixtures
{
    public static class Receivers
    {
        public static Receive EmptyReceive = c => Actor.Done;
    }
}