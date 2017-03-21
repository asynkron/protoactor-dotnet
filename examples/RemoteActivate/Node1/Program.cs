// -----------------------------------------------------------------------
//  <copyright file="Program.cs" company="Asynkron HB">
//      Copyright (C) 2015-2017 Asynkron HB All rights reserved
//  </copyright>
// -----------------------------------------------------------------------

using System;
using System.Threading;
using System.Threading.Tasks;
using Messages;
using Proto;
using Proto.Remote;
using ProtosReflection = Messages.ProtosReflection;

class Program
{
    static void Main(string[] args)
    {
        Serialization.RegisterFileDescriptor(ProtosReflection.Descriptor);
        Remote.Start("127.0.0.1", 12001);
        var pid = Remote.SpawnNamedAsync("127.0.0.1:12000", "remote", "hello", TimeSpan.FromSeconds(5)).Result;
        var res = pid.RequestAsync<HelloResponse>(new HelloRequest { }).Result;
        Console.WriteLine(res.Message);
        Console.ReadLine();
    }
}