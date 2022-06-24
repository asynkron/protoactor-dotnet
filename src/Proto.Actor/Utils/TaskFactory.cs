// -----------------------------------------------------------------------
// <copyright file="TaskFactory.cs" company="Asynkron AB">
//      Copyright (C) 2015-2022 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------
using System;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Proto;

public static class SafeTask
{
    private static readonly ILogger Logger = Log.CreateLogger<TaskFactory>();

    /// <summary>
    /// Runs a task and handles exceptions. If <see cref="TaskCanceledException"/> is thrown, it is ignored.
    /// If any other exception is thrown, it is logged.
    /// </summary>
    /// <param name="body"></param>
    /// <param name="cancellationToken"></param>
    /// <param name="name"></param>
    public static async Task Run(Func<Task> body, CancellationToken cancellationToken = default, [CallerMemberName] string name = "")
    {
        Task? t = null;
        try
        {
            t = Task.Run(body, cancellationToken);
            await t;
        }
        catch (TaskCanceledException e) when (e.Task == t)
        {
            //pass. do not log if our own task was cancelled
        }
        catch (Exception x)
        {
            x.CheckFailFast();
            Logger.LogError(x, "Unhandled exception in async job {Job}", name);
        }
    }
}