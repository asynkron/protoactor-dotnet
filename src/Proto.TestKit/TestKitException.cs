// -----------------------------------------------------------------------
// <copyright file="TestKitException.cs" company="Asynkron AB">
//      Copyright (C) 2015-2022 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------
using System;

namespace Proto.TestKit
{
    public class TestKitException : Exception
    {
        public TestKitException(string message) : base(message)
        {
        }
    }
}