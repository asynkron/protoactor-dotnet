// -----------------------------------------------------------------------
// <copyright file="TestProviderOptions.cs" company="Asynkron AB">
//      Copyright (C) 2015-2020 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------
using System;

namespace Proto.Cluster.Testing
{
    public class TestProviderOptions
    {
        /// <summary>
        ///     Default value is 3 seconds
        /// </summary>
        public TimeSpan ServiceTtl { get; set; } = TimeSpan.FromSeconds(10);

        /// <summary>
        ///     Default value is 1 second
        /// </summary>
        public TimeSpan RefreshTtl { get; set; } = TimeSpan.FromSeconds(1);

        /// <summary>
        ///     Default value is 10 seconds
        /// </summary>
        public TimeSpan DeregisterCritical { get; set; } = TimeSpan.FromSeconds(30);
    }
}