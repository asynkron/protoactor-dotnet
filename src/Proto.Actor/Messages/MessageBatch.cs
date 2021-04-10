// -----------------------------------------------------------------------
// <copyright file="MessageBatch.cs" company="Asynkron AB">
//      Copyright (C) 2015-2021 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------
using System.Collections.Generic;

namespace Proto
{
    public record MessageBatch(IReadOnlyCollection<object> Messages);
}