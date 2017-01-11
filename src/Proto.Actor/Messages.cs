// -----------------------------------------------------------------------
//  <copyright file="Messages.cs" company="Asynkron HB">
//      Copyright (C) 2015-2016 Asynkron HB All rights reserved
//  </copyright>
// -----------------------------------------------------------------------

using System;

namespace Proto
{
    public abstract class SystemMessage
    {
    }

    public abstract class AutoReceiveMessage
    {
    }

    public sealed partial class Terminated : SystemMessage
    {
    }

    public sealed class SuspendMailbox : SystemMessage
    {
        public static readonly SuspendMailbox Instance = new SuspendMailbox();

        private SuspendMailbox()
        {
        }
    }

    public sealed class ResumeMailbox : SystemMessage
    {
        public static readonly ResumeMailbox Instance = new ResumeMailbox();

        private ResumeMailbox()
        {
        }
    }

    public class Failure : SystemMessage
    {
        public Failure(PID who, Exception reason)
        {
            Who = who;
            Reason = reason;
        }

        public Exception Reason { get; }
        public PID Who { get; }
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
        public static readonly Restart Instance = new Restart();
    }

    public sealed class Stop : SystemMessage
    {
        public static readonly Stop Instance = new Stop();
    }

    public sealed partial class Stopping : AutoReceiveMessage
    {
        public static readonly Stopping Instance = new Stopping();
    }

    public sealed partial class Started : AutoReceiveMessage
    {
        public static readonly Started Instance = new Started();
    }

    public sealed partial class Stopped : AutoReceiveMessage
    {
        public static readonly Stopped Instance = new Stopped();
    }
}