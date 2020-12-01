// -----------------------------------------------------------------------
// <copyright file="Exceptions.cs" company="Asynkron AB">
//      Copyright (C) 2015-2020 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------
using System;

namespace Proto
{
    public class ProcessNameExistException : Exception
    {
        public ProcessNameExistException(string name, PID pid) : base($"a Process with the name '{name}' already exists"
        )
        {
            Name = name;
            Pid = pid;
        }

        public string Name { get; }
        public PID Pid { get; }
    }
}