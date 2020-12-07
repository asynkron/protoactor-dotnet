// -----------------------------------------------------------------------
// <copyright file="Counter.cs" company="Asynkron AB">
//      Copyright (C) 2015-2020 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------
using JetBrains.Annotations;

namespace Proto.Metrics
{
    [PublicAPI]
    public interface ICounter
    {
        string Name { get; }
        string Description { get; }
        void Inc();
    }

    [PublicAPI]
    public class Counter
        : ICounter
    {
        public Counter(string name, string description)
        {
            Name = name;
            Description = description;
        }

        public string Name { get; }

        public string Description { get; }

        public void Inc(){
        
        }
    }
}