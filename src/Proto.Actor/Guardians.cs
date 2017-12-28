// -----------------------------------------------------------------------
//   <copyright file="Guardians.cs" company="Asynkron HB">
//       Copyright (C) 2015-2017 Asynkron HB All rights reserved
//   </copyright>
// -----------------------------------------------------------------------

using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace Proto
{
    internal static class Guardians
    {
        private static ConcurrentDictionary<ISupervisorStrategy, GuardianProcess> _guardians = new ConcurrentDictionary<ISupervisorStrategy, GuardianProcess>();

        internal static PID GetGuardianPID(ISupervisorStrategy strategy)
        {
            if (!_guardians.TryGetValue(strategy, out var guardian))
            {
                guardian = new GuardianProcess(strategy);
                _guardians[strategy] = guardian;
            }
            return guardian.Pid;
        }
    }

    internal class GuardianProcess : Process, ISupervisor
    {
        public PID Pid { get; }

        public IReadOnlyCollection<PID> Children => throw new MemberAccessException("Guardian does not hold its children PIDs.");

        private readonly ISupervisorStrategy _supervisorStrategy;

        internal GuardianProcess(ISupervisorStrategy strategy)
        {
            _supervisorStrategy = strategy;

            var name = $"Guardian{ProcessRegistry.Instance.NextId()}";
            var (pid, absent) = ProcessRegistry.Instance.TryAdd(name, this);
            if (!absent)
            {
                throw new ProcessNameExistException(name, pid);
            }
            Pid = pid;
        }

        protected internal override void SendUserMessage(PID pid, object message)
        {
            throw new InvalidOperationException($"Guardian actor cannot receive any user messages.");
        }

        protected internal override void SendSystemMessage(PID pid, object message)
        {
            if (message is Failure)
            {
                var msg = message as Failure;
                _supervisorStrategy.HandleFailure(this, msg.Who, msg.RestartStatistics, msg.Reason);
            }
        }

        public void EscalateFailure(Exception reason, PID who)
        {
            throw new InvalidOperationException("Guardian cannot escalate failure.");
        }

        public void RestartChildren(Exception reason, params PID[] pids)
        {
            foreach (var pid in pids)
            {
                pid.SendSystemMessage(new Restart(reason));
            }
        }

        public void StopChildren(params PID[] pids)
        {
            foreach (var pid in pids)
            {
                pid.SendSystemMessage(Proto.Stop.Instance);
            }
        }

        public void ResumeChildren(params PID[] pids)
        {
            foreach (var pid in pids)
            {
                pid.SendSystemMessage(Proto.Mailbox.ResumeMailbox.Instance);
            }
        }
    }
}