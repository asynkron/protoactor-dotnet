// -----------------------------------------------------------------------
//   <copyright file="Guardians.cs" company="Asynkron HB">
//       Copyright (C) 2015-2018 Asynkron HB All rights reserved
//   </copyright>
// -----------------------------------------------------------------------

using System;
using System.Collections.Concurrent;
using System.Collections.Immutable;
using Proto.Mailbox;

namespace Proto
{
    internal static class Guardians
    {
        private static readonly ConcurrentDictionary<ISupervisorStrategy, GuardianProcess> GuardianStrategies =
            new ConcurrentDictionary<ISupervisorStrategy, GuardianProcess>();

        internal static PID GetGuardianPID(ISupervisorStrategy strategy)
        {
            GuardianProcess ValueFactory(ISupervisorStrategy s) => new GuardianProcess(s);

            var guardian = GuardianStrategies.GetOrAdd(strategy, ValueFactory);
            return guardian.Pid;
        }
    }

    internal class GuardianProcess : Process, ISupervisor
    {
        private readonly ISupervisorStrategy _supervisorStrategy;

        internal GuardianProcess(ISupervisorStrategy strategy)
        {
            _supervisorStrategy = strategy;

            var name = $"Guardian{ProcessRegistry.Instance.NextId()}";
            var (pid, ok) = ProcessRegistry.Instance.TryAdd(name, this);
            if (!ok)
            {
                throw new ProcessNameExistException(name, pid);
            }

            Pid = pid;
        }

        public PID Pid { get; }

        public IImmutableSet<PID> Children =>
            throw new MemberAccessException("Guardian does not hold its children PIDs.");

        public void EscalateFailure(Exception reason, PID who)
        {
            throw new InvalidOperationException("Guardian cannot escalate failure.");
        }

        public void RestartChildren(Exception reason, params PID[] pids) =>
            pids?.SendSystemNessage(new Restart(reason));

        public void StopChildren(params PID[] pids) => pids?.Stop();

        public void ResumeChildren(params PID[] pids) => pids?.SendSystemNessage(ResumeMailbox.Instance);

        protected internal override void SendUserMessage(PID pid, object message)
        {
            throw new InvalidOperationException($"Guardian actor cannot receive any user messages.");
        }

        protected internal override void SendSystemMessage(PID pid, object message)
        {
            if (message is Failure msg)
            {
                _supervisorStrategy.HandleFailure(this, msg.Who, msg.RestartStatistics, msg.Reason);
            }
        }
    }
}