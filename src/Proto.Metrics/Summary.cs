// -----------------------------------------------------------------------
// <copyright file="Summary.cs" company="Asynkron AB">
//      Copyright (C) 2015-2020 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------
using JetBrains.Annotations;

namespace Proto.Metrics
{
    [PublicAPI]
    public interface ISummary
    {
        string Name { get; }
        string Description { get; }

        public void Observe(double value);
    }

    
    public class Summary : ISummary
    {
        public Summary(string name, string description)
        {
            Name = name;
            Description = description;
        }

        public string Name { get; }

        public string Description { get; }

        public void Observe(double value)
        {
            
        }
    }
}