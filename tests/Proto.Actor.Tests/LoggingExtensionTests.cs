// -----------------------------------------------------------------------
// <copyright file="LoggingExtensionTests.cs" company="Asynkron AB">
//      Copyright (C) 2015-2021 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------
using Microsoft.Extensions.Logging;
using Proto.Logging;
using Xunit;

namespace Proto.Tests
{
    public class LoggingExtensionTests
    {
        [Fact]
        public void CanGetLoggingExtension()
        {
            var system = new ActorSystem();
            system.Extensions.Register(new InstanceLogger(LogLevel.Debug));

            var logger = system.Extensions.Get<InstanceLogger>();

            Assert.NotNull(logger);
        }

        [Fact]
        public void CanLogToLogStore()
        {
            var system = new ActorSystem();
            var logStore = new LogStore();
            system.Extensions.Register(new InstanceLogger(LogLevel.Debug, logStore));

            var logger = system.Extensions.Get<InstanceLogger>();

            Assert.Empty(logStore.GetEntries());
            logger?.LogDebug("Hello {World}", "World");

            Assert.Single(logStore.GetEntries());
        }
        
        [Fact]
        public void CanCompareEntriesInStore()
        {
            var system = new ActorSystem();
            var logStore = new LogStore();
            system.Extensions.Register(new InstanceLogger(LogLevel.Debug, logStore));

            var logger = system.Extensions.Get<InstanceLogger>();
            
            logger?.LogDebug("...123....Hello...456...");
            logger?.LogDebug("...789....World...012...");

            var hello = logStore.FindEntry("Hello");
            Assert.NotNull(hello);
            
            var world = logStore.FindEntry("World");
            Assert.NotNull(world);
            
            Assert.True(hello.IsBefore(world));
        }
    }
}