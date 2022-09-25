// -----------------------------------------------------------------------
// <copyright file="Exceptions.cs" company="Asynkron AB">
//      Copyright (C) 2015-2022 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------

using System;

namespace Proto;

#pragma warning disable RCS1194
public class ProcessNameExistException : Exception
#pragma warning restore RCS1194
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