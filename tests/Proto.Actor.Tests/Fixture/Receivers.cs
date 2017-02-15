// -----------------------------------------------------------------------
//  <copyright file="ActorFixture.cs" company="Asynkron HB">
//      Copyright (C) 2015-2017 Asynkron HB All rights reserved
//  </copyright>
// -----------------------------------------------------------------------


namespace Proto.Tests.Fixture
{
    static class Receivers
    {
        public static Receive EmptyReceive = c => Actor.Done;
    }
}