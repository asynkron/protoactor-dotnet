// -----------------------------------------------------------------------
// <copyright file="IActor.cs" company="Asynkron AB">
//      Copyright (C) 2015-2020 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------
using System.Threading.Tasks;

namespace Proto
{
    public interface IActor
    {
        Task ReceiveAsync(IContext context);
    }
}