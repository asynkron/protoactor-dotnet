// -----------------------------------------------------------------------
// <copyright file="Program.cs" company="Asynkron AB">
//      Copyright (C) 2015-2020 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------
using System.CommandLine;
using System.Threading.Tasks;

namespace ProtoGrainGenerator
{
    internal class Program
    {
        private static Task Main(string[] args) => Commands.CreateCommands().InvokeAsync(args);
    }
}