// -----------------------------------------------------------------------
// <copyright file="Runner.cs" company="Asynkron AB">
//      Copyright (C) 2015-2020 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------
using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Proto.Cluster;

namespace ClusterExperiment1
{
    public interface IRunMember
    {
        Task Start();

        Task Kill();
    }

    public class RunMemberInProcGraceful : IRunMember
    {
        private Cluster _cluster;

        public async Task Start() => _cluster = await Configuration.SpawnMember();

        public async Task Kill() => await _cluster.ShutdownAsync(true);
    }
    
    public class RunMemberInProc : IRunMember
    {
        private Cluster _cluster;

        public async Task Start() => _cluster = await Configuration.SpawnMember();

        public async Task Kill() => await _cluster.ShutdownAsync(false);
    }

    public class RunMemberExternalProcGraceful : IRunMember
    {
        public static readonly string SelfPath = typeof(Program).Assembly.Location;
        private Process _process;

        public Task Start()
        {
            Console.WriteLine("Starting external worker");
            var l = typeof(Program).Assembly.Location;

            _process = Process.Start(new ProcessStartInfo("dotnet", $"{l} worker"));
            
            return Task.CompletedTask;
        }

        public Task Kill()
        {
            Console.WriteLine("Killing external worker");
            Process.Start("kill", $"-s TERM {_process.Id}");
            //_process.Kill(false);
            return Task.CompletedTask;
        }
    }
    
    public class RunMemberExternalProc : IRunMember
    {
        public static readonly string SelfPath = typeof(Program).Assembly.Location;
        private Process _process;

        public Task Start()
        {
            Console.WriteLine("Starting external worker");
            var l = typeof(Program).Assembly.Location;

            _process = Process.Start(new ProcessStartInfo("dotnet", $"{l} worker"));
            
            return Task.CompletedTask;
        }

        public Task Kill()
        {
            Console.WriteLine("Killing external worker");
            _process.Kill(true);
            return Task.CompletedTask;
        }
    }
}