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

    public sealed partial class SuspendMailbox : SystemMessage
    {
    }

    public sealed partial class ResumeMailbox : SystemMessage
    {
    }

    public class Failure : SystemMessage
    {

        public Failure(PID who, Exception reason)
        {
            this.Who = who;
            this.Reason = reason;
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

    public sealed partial class Restart : SystemMessage
    {
        public static readonly Restart Instance = new Restart();
    }

    public sealed partial class Stop : SystemMessage
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