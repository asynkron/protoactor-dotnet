// -----------------------------------------------------------------------
// <copyright file="PID.cs" company="Asynkron AB">
//      Copyright (C) 2015-2020 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------
using Google.Protobuf;

namespace Proto
{
    // ReSharper disable once InconsistentNaming
    public partial class PID : ICustomDiagnosticMessage
    {
        private Process? _process;

        public PID(string address, string id)
        {
            Address = address;
            Id = id;
        }

        internal PID(string address, string id, Process process) : this(address, id) => _process = process;

        public string ToDiagnosticString() => $"{Address}/{Id}";

        public static PID FromAddress(string address, string id) => new(address, id);

        internal Process? Ref(ActorSystem system)
        {
            if (_process is not null)
            {
                if (_process is ActorProcess actorProcess && actorProcess.IsDead) _process = null;

                return _process;
            }

            var reff = system.ProcessRegistry.Get(this);
            if (reff is not DeadLetterProcess) _process = reff;

            return _process;
        }

        internal void SendUserMessage(ActorSystem system, object message)
        {
            var reff = Ref(system) ?? system.ProcessRegistry.Get(this);
            reff.SendUserMessage(this, message);
        }

        public void SendSystemMessage(ActorSystem system, object sys)
        {
            var reff = Ref(system) ?? system.ProcessRegistry.Get(this);
            reff.SendSystemMessage(this, sys);
        }

        public void Stop(ActorSystem system)
        {
            var reff = _process ?? system.ProcessRegistry.Get(this);
            reff.Stop(this);
        }

        public PID WithRequestId(uint requestId) => new()
        {
            Id = Id,
            Address = Address,
            _process = _process,
            RequestId = requestId
        };
    }
}