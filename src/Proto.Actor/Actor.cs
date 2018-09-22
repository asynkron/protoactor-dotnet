// -----------------------------------------------------------------------
//   <copyright file="Actor.cs" company="Asynkron HB">
//       Copyright (C) 2015-2018 Asynkron HB All rights reserved
//   </copyright>
// -----------------------------------------------------------------------

using System;
using System.Threading.Tasks;

namespace Proto
{
    public static class Actor
    {
        public static readonly Task Done = Task.FromResult(0);
        public static EventStream EventStream => EventStream.Instance;
    }
}