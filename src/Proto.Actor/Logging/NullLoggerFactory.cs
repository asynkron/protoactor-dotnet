// -----------------------------------------------------------------------
// <copyright file="NullLoggerFactory.cs" company="Asynkron AB">
//      Copyright (C) 2015-2020 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------
using System;
using JetBrains.Annotations;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Proto.Logging
{
    [PublicAPI]
    public sealed class NullLoggerFactory : ILoggerFactory
    {
        public ILogger CreateLogger(string name) => NullLogger.Instance;

        public void AddProvider(ILoggerProvider provider)
        {
        }

        // ReSharper disable once CA1816
        public void Dispose()
        {
        }
    }
}