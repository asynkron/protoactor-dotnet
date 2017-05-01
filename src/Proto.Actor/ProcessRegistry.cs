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

        private readonly ProcessHost _defaultHost = new ProcessHost(NoHost);
        public IProcessHost DefaultHost => _defaultHost;

        private readonly IList<IProcessHost> _processHosts;
        public void RegisterProcessHost(IProcessHost host) => _processHosts.Add(host);

        private readonly IList<Func<PID, Process>> _hostResolvers = new List<Func<PID, Process>>();
        public void RegisterHostResolver(Func<PID, Process> resolver) => _hostResolvers.Add(resolver);

        public ProcessRegistry()
        {
            _processHosts = new List<IProcessHost> {_defaultHost};
        }

        public IProcessHost GetProcessHost(PID pid)
        {
            if (pid.Address == NoHost)
                return _defaultHost;

            foreach (var host in _processHosts)
            {
                if (pid.Address == host.Address)
                    return host;
            }

            return null;
        }

        public Process Get(PID pid)
        {
            var processHost = GetProcessHost(pid);
            if (processHost != null)
                return processHost.Get(pid.Id);

            foreach (var resolver in _hostResolvers)
            {
                var process = resolver(pid);
                if (process != null)
                    return process;
            }

            throw new NotSupportedException("Unknown host");
        }

        public static ProcessRegistry Instance { get; } = new ProcessRegistry();

        public string Address
        {
            get => _defaultHost.Address;
            set => _defaultHost.Address = value;
        }

        #region Backward compatibility
        public string NextId() => _defaultHost.NextId();
        public (PID pid, bool ok) TryAdd(string name, Process aref) => _defaultHost.TryAdd(name, aref);
        public void Remove(PID pid) => GetProcessHost(pid).Remove(pid.Id);
        #endregion
    }

    // A IProcessHost holds the references to the Process object of each actor
    // belonging to a certain Host address.
    public interface IProcessHost
    {
        string Address { get; }

        Process Get(string id);
        (PID pid, bool ok) TryAdd(string id, Process aref);
        void Remove(string id);

        string NextId();
    }

    public class ProcessHost : IProcessHost
    {
        public string Address { get; set; }

        public ProcessHost(string address) => Address = address;

        private int _sequenceId;
        private readonly HashedConcurrentDictionary _localActorRefs =
            new HashedConcurrentDictionary();

        public Process Get(string id)
        {
            if (!_localActorRefs.TryGetValue(id, out var aref))
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

        public void Remove(string id)
        {
            _localActorRefs.Remove(id);
        }

        public string NextId()
        {
            var counter = Interlocked.Increment(ref _sequenceId);
            return "$" + counter;
        }
    }
}