// -----------------------------------------------------------------------
//   <copyright file="Messages.cs" company="Asynkron AB">
//       Copyright (C) 2015-2020 Asynkron AB All rights reserved
//   </copyright>
// -----------------------------------------------------------------------

using System;
using System.Threading.Tasks;
using Proto.Mailbox;

// ReSharper disable once CheckNamespace
namespace Proto
{
    public sealed partial class Terminated : SystemMessage
    {
    }
    
    public sealed class Restarting
    {
        public static readonly Restarting Instance = new Restarting();

        private Restarting()
        {
        }
    }
    
    public sealed partial class PoisonPill
    {
        public static readonly PoisonPill Instance = new PoisonPill();
    }

    public class Failure : SystemMessage
    {
        public Failure(PID who, Exception reason, RestartStatistics crs, object? message)
        {
            Who = who;
            Reason = reason;
            RestartStatistics = crs;
            Message = message;
        }

        public Exception Reason { get; }
        public PID Who { get; }
        public RestartStatistics RestartStatistics { get; }
        public object? Message { get; }
    }

    public sealed partial class Watch : SystemMessage
    {
        public Watch(PID watcher)
        {
            Watcher = watcher;
        }
    }

    public sealed partial class Unwatch : SystemMessage
    {
        public Unwatch(PID watcher)
        {
            Watcher = watcher;
        }
    }

    public sealed class Restart : SystemMessage
    {
        public Restart(Exception reason)
        {
            Reason = reason;
        }

        public Exception Reason { get; }
    }

    public partial class Stop : SystemMessage
    {
        public static readonly Stop Instance = new Stop();
    }

    public sealed class Stopping : SystemMessage
    {
        public static readonly Stopping Instance = new Stopping();

        private Stopping()
        {
        }
    }

    public sealed class Started : SystemMessage
    {
        public static readonly Started Instance = new Started();

        private Started()
        {
        }
    }

    public sealed class Stopped : SystemMessage
    {
        public static readonly Stopped Instance = new Stopped();

        private Stopped()
        {
        }
    }

    public class ReceiveTimeout : SystemMessage
    {
        public static readonly ReceiveTimeout Instance = new ReceiveTimeout();

        private ReceiveTimeout()
        {
        }
    }
    
    public interface INotInfluenceReceiveTimeout
    {
    }

    public class Continuation : SystemMessage
    {
        public Continuation(Func<Task>? fun, object? message)
        {
            Action = fun ?? throw new ArgumentNullException(nameof(fun));
            Message = message ?? throw new ArgumentNullException(nameof(message));
        }

        public Func<Task> Action { get; }
        public object Message { get; }
    }
}