// -----------------------------------------------------------------------
// <copyright file="ContextState.cs" company="Asynkron AB">
//      Copyright (C) 2015-2020 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------

// ReSharper disable once CheckNamespace
namespace Proto
{
    //the lifecycle of an actor
    enum ContextState : byte
    {
        Alive,      //default state
        Restarting, //after being restarted by supervisor
        Stopping,   //initiating stop, all context members still functional
        Stopped     //actor is fully stopped, Self PID goes to DeadLetter
    }
}