// -----------------------------------------------------------------------
// <copyright file="FutureProcessTests.cs" company="Asynkron AB">
//      Copyright (C) 2015-2021 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------
using Proto.Future;

namespace Proto.Tests
{
    // ReSharper disable once UnusedType.Global
    public class FutureProcessTests : BaseFutureTests
    {
        protected override IFuture GetFuture() => new FutureProcess(System);
    }
}