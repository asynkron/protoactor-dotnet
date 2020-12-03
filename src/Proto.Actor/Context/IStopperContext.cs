// -----------------------------------------------------------------------
// <copyright file="IStopperContext.cs" company="Asynkron AB">
//      Copyright (C) 2015-2020 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------
using System.Threading.Tasks;

// ReSharper disable once CheckNamespace
namespace Proto
{
    public interface IStopperContext
    {
        /// <summary> Stop will tell actor to stop immediately, regardless of existing user messages in mailbox. </summary>
        void Stop(PID pid);

        /// <summary> StopAsync will tell and wait actor to stop immediately, regardless of existing user messages in mailbox. </summary>
        Task StopAsync(PID pid);

        /// <summary> Poison will tell actor to stop after processing current user messages in mailbox. </summary>
        void Poison(PID pid);

        /// <summary> PoisonAsync will tell and wait actor to stop after processing current user messages in mailbox. </summary>
        Task PoisonAsync(PID pid);
    }
}