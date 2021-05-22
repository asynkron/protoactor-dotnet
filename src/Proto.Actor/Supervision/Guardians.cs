// -----------------------------------------------------------------------
// <copyright file="Guardians.cs" company="Asynkron AB">
//      Copyright (C) 2015-2020 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------
using System;
using System.Collections.Concurrent;
using System.Collections.Immutable;
using Proto.Mailbox;

// ReSharper disable once CheckNamespace
namespace Proto
{
    public class Guardians
    {
        private readonly ConcurrentDictionary<ISupervisorStrategy, GuardianProcess> _guardianStrategies = new();

        public Guardians(ActorSystem system) => System = system;

        private ActorSystem System { get; }

        internal PID GetGuardianPid(ISupervisorStrategy strategy)
        {
            GuardianProcess ValueFactory(ISupervisorStrategy s) => new(System, s);

            var guardian = _guardianStrategies.GetOrAdd(strategy, ValueFactory);
            return guardian.Pid;
        }
    }

    class GuardianProcess : Process, ISupervisor
    {
        private readonly ISupervisorStrategy _supervisorStrategy;

        internal GuardianProcess(ActorSystem system, ISupervisorStrategy strategy) : base(system)
        {
            _supervisorStrategy = strategy;

            var name = $"Guardian{System.ProcessRegistry.NextId()}";
            var (pid, ok) = System.ProcessRegistry.TryAdd(name, this);

            if (!ok) throw new ProcessNameExistException(name, pid);

            Pid = pid;
        }

        public PID Pid { get; }

        public IImmutableSet<PID> Children =>
            throw new MemberAccessException("Guardian does not hold its children PIDs.");

        public void EscalateFailure(Exception reason, object? message) =>
            throw new InvalidOperationException("Guardian cannot escalate failure.");

        public void RestartChildren(Exception reason, params PID[] pids) =>
            pids.SendSystemMessage(new Restart(reason), System);

        public void StopChildren(params PID[] pids) => pids.Stop(System);

        public void ResumeChildren(params PID[] pids) => pids.SendSystemMessage(ResumeMailbox.Instance, System);

        protected internal override void SendUserMessage(PID pid, object message)
            => throw new InvalidOperationException("Guardian actor cannot receive any user messages.");

        protected internal override void SendSystemMessage(PID pid, object message)
        {
            if (message is Failure msg)
                _supervisorStrategy.HandleFailure(this, msg.Who, msg.RestartStatistics, msg.Reason, msg.Message);
        }
    }
}