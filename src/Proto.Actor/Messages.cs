// -----------------------------------------------------------------------
//   <copyright file="Messages.cs" company="Asynkron HB">
//       Copyright (C) 2015-2018 Asynkron HB All rights reserved
//   </copyright>
// -----------------------------------------------------------------------

using System;
using System.Threading.Tasks;
using Proto.Mailbox;

namespace Proto
{
    public abstract class AutoReceiveMessage
    {}

    public sealed partial class Terminated : SystemMessage
    {}

    public sealed class Restarting
    {
        public static readonly Restarting Instance = new Restarting();

        private Restarting()
        {
        }
    }

    public class Failure : SystemMessage
    {
        public Failure(PID who, Exception reason, RestartStatistics crs)
        {
            Who = who;
            Reason = reason;
            RestartStatistics = crs;
        }

        public Exception Reason { get; }
        public PID Who { get; }
        public RestartStatistics RestartStatistics { get; }
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

    public sealed class Stopping : AutoReceiveMessage
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

    public sealed class Stopped : AutoReceiveMessage
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
    {}

    public class Continuation : SystemMessage
    {
        public Continuation(Func<Task> fun, object message)
        {
            Action = fun ?? throw new ArgumentNullException(nameof(fun));
            Message = message ?? throw new ArgumentNullException(nameof(message));
        }

        public Func<Task> Action { get;  }
        public object Message { get; }
    }
}