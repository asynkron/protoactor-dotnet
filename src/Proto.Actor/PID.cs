// -----------------------------------------------------------------------
//   <copyright file="PID.cs" company="Asynkron HB">
//       Copyright (C) 2015-2018 Asynkron HB All rights reserved
//   </copyright>
// -----------------------------------------------------------------------

namespace Proto
{
    // ReSharper disable once InconsistentNaming
    public partial class PID
    {
        private Process? _process;

        public PID(string address, string id)
        {
            Address = address;
            Id = id;
        }

        internal PID(string address, string id, Process process) : this(address, id)
        {
            _process = process;
        }

        internal Process? Ref(ActorSystem system)
        {

            var p = _process;
            if (p != null)
            {
                if (p is ActorProcess lp && lp.IsDead)
                {
                    _process = null;
                }

                return _process;
            }

            var reff = system.ProcessRegistry.Get(this);
            if (!(reff is DeadLetterProcess))
            {
                _process = reff;
            }

            return _process;

        }

        internal void SendUserMessage(ActorSystem system, object message)
        {
            var reff = Ref(system) ?? system.ProcessRegistry.Get(this);
            reff.SendUserMessage(this, message);
        }

        public void SendSystemMessage(ActorSystem system,object sys)
        {
            var reff = Ref(system) ?? system.ProcessRegistry.Get(this);
            reff.SendSystemMessage(this, sys);
        }

        public string ToShortString() => Address + "/" + Id;
    }
}