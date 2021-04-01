// -----------------------------------------------------------------------
// <copyright file="DSL.cs" company="Asynkron AB">
//      Copyright (C) 2015-2021 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------

using JetBrains.Annotations;

namespace Proto.DSL
{
    [PublicAPI]
    public class Functions
    {
        public static T[] ArrayOf<T>(params T[] elements) => elements;
    }
}
