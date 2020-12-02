// -----------------------------------------------------------------------
// <copyright file="TestKit.cs" company="Asynkron AB">
//      Copyright (C) 2015-2020 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------
namespace Proto.TestKit
{
    public static class TestKit
    {
        public static ActorSystem System { get; } = new();
    }
}