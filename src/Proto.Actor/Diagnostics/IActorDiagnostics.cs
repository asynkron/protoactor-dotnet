// -----------------------------------------------------------------------
// <copyright file="IActorDiagnostics.cs" company="Asynkron AB">
//      Copyright (C) 2015-2022 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------

namespace Proto.Diagnostics;

/// <summary>
///     Adds the ability to return a diagnostic string for an actor instance
/// </summary>
public interface IActorDiagnostics
{
    /// <summary>
    ///     Return a diagnostic string for the actor
    /// </summary>
    /// <returns></returns>
    string GetDiagnosticsString();
}