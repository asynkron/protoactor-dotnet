// -----------------------------------------------------------------------
//   <copyright file="PID.cs" company="Asynkron HB">
//       Copyright (C) 2015-2018 Asynkron HB All rights reserved
//   </copyright>
// -----------------------------------------------------------------------

using System.Threading.Tasks;

namespace Proto
{
    // ReSharper disable once InconsistentNaming
    public partial class PID
    {
        private Process _process;

        public PID(string address, string id)
        {
            Address = address;
            Id = id;
        }

        internal PID(string address, string id, Process process) : this(address, id) => _process = process;

        internal Process Ref
        {
            get
            {
                if (_process != null)
                {
                    if (_process is ActorProcess actorProcess && actorProcess.IsDead)
                    {
                        _process = null;
                    }
                    return _process;
                }

                var process = ProcessRegistry.Instance.Get(this);
                if (!(process is DeadLetterProcess))
                {
                    _process = process;
                }

                return _process;
            }
        }

        internal void SendUserMessage(object message)
        {
            var process = Ref ?? ProcessRegistry.Instance.Get(this);
            process.SendUserMessage(this, message);
        }

        public void SendSystemMessage(object sys)
        {
            var process = Ref ?? ProcessRegistry.Instance.Get(this);
            process.SendSystemMessage(this, sys);
        }

        /// <summary> Stop will tell actor to stop immediately, regardless of existing user messages in mailbox. </summary>
        public void Stop() => ProcessRegistry.Instance.Get(this).Stop(this);

        /// <summary> StopAsync will tell and wait actor to stop immediately, regardless of existing user messages in mailbox. </summary>
        public Task StopAsync()
        {
            var future = new FutureProcess<object>();

            SendSystemMessage(new Watch(future.Pid));
            Stop();

            return future.Task;
        }

        /// <summary> Poison will tell actor to stop after processing current user messages in mailbox. </summary>
        public void Poison() => SendUserMessage(new PoisonPill());

        /// <summary> PoisonAsync will tell and wait actor to stop after processing current user messages in mailbox. </summary>
        public Task PoisonAsync()
        {
            var future = new FutureProcess<object>();

            SendSystemMessage(new Watch(future.Pid));
            Poison();            

            return future.Task;
        }

        public string ToShortString() => Address + "/" + Id;
    }
}