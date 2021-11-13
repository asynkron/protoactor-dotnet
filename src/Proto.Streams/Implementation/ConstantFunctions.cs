//-----------------------------------------------------------------------
// <copyright file="ConstantFunctions.cs" company="Proto.NET Project">
//     Copyright (C) 2009-2021 Lightbend Inc. <http://www.lightbend.com>
//     Copyright (C) 2013-2021 .NET Foundation <https://github.com/akkadotnet/akka.net>
// </copyright>
//-----------------------------------------------------------------------

using System;

namespace Proto.Streams.Implementation
{
    /// <summary>
    /// TBD
    /// </summary>
    internal static class ConstantFunctions
    {
        /// <summary>
        /// TBD
        /// </summary>
        /// <typeparam name="T">TBD</typeparam>
        /// <returns>TBD</returns>
        public static Func<T, long> OneLong<T>() => _ => 1L;
    }
}
