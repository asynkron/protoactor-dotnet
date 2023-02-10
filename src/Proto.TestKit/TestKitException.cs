// -----------------------------------------------------------------------
// <copyright file="TestKitException.cs" company="Asynkron AB">
//      Copyright (C) 2015-2022 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------

using System;

namespace Proto.TestKit;

#pragma warning disable RCS1194
public class TestKitException : Exception
#pragma warning restore RCS1194
{
    public TestKitException(string message) : base(message)
    {
    }
}