// -----------------------------------------------------------------------
// <copyright file="Gauge.cs" company="Asynkron AB">
//      Copyright (C) 2015-2020 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------
using JetBrains.Annotations;

namespace Proto.Metrics
{
    [PublicAPI]
    public interface IGauge
    {
        string Name { get; }
        string Description { get; }
        void Inc();
        void Dec();
    }

    [PublicAPI]
    public class Gauge
        : IGauge
    {
        public Gauge(string name, string description)
        {
            Name = name;
            Description = description;
        }

        public string Name { get; }

        public string Description { get; }

        public void Inc(){
        
        }
        
        public void Dec(){
        
        }
    }
}