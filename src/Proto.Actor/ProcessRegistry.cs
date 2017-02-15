// -----------------------------------------------------------------------
//  <copyright file="ProcessRegistry.cs" company="Asynkron HB">
//      Copyright (C) 2015-2017 Asynkron HB All rights reserved
//  </copyright>
// -----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Threading;

namespace Proto
{
    public class ProcessRegistry
    {
        private const string NoHost = "nonhost";
        private readonly IList<Func<PID, Process>> _hostResolvers = new List<Func<PID, Process>>();

        private readonly HashedConcurrentDictionary _localActorRefs =
            new HashedConcurrentDictionary();

        private int _sequenceId;
        public static ProcessRegistry Instance { get; } = new ProcessRegistry();

        public string Address { get; set; } = NoHost;

        public void RegisterHostResolver(Func<PID, Process> resolver)
        {
            _hostResolvers.Add(resolver);
        }

        public Process Get(PID pid)
        {
            if (pid.Address != "nonhost" && pid.Address != Address)
            {
                foreach (var resolver in _hostResolvers)
                {
                    var reff = resolver(pid);
                    if (reff == null)
                    {
                        continue;
                    }
                    return reff;
                }
                throw new NotSupportedException("Unknown host");
            }

            if (!_localActorRefs.TryGetValue(pid.Id, out var aref))
            {
                return DeadLetterProcess.Instance;
            }
            return aref;
        }

        public (PID pid, bool ok) TryAdd(string id, Process aref)
        {
            var pid = new PID
            {
                Id = id,
                Address = Address // local
            };
            var ok = _localActorRefs.TryAdd(pid.Id, aref);
            return (pid, ok);
        }

        public void Remove(PID pid)
        {
            _localActorRefs.Remove(pid.Id);
        }

        public string NextId()
        {
            var counter = Interlocked.Increment(ref _sequenceId);
            return "$" + counter;
        }
    }
}