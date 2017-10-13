// -----------------------------------------------------------------------
//   <copyright file="Program.cs" company="Asynkron HB">
//       Copyright (C) 2015-2017 Asynkron HB All rights reserved
//   </copyright>
// -----------------------------------------------------------------------

using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Messages;
using Proto.Cluster;
using Proto.Cluster.Consul;
using Proto.Remote;
using ProtosReflection = Messages.ProtosReflection;

namespace TestApp
{

    class Program
    {


        static void Main(string[] args)
        {
            if (args == null || args.Length == 0)
            {
                Client.Start();
            }
            else
            {
                Worker.Start();
            }
        }
    }
}