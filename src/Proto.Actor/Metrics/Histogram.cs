// -----------------------------------------------------------------------
// <copyright file="Histogram.cs" company="Asynkron AB">
//      Copyright (C) 2015-2020 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------
using JetBrains.Annotations;

namespace Proto.Metrics
{
    [PublicAPI]
    public interface IHistogram
    {
        string Name { get; }
        string Description { get; }

        void Observe(double val);
        void Observe(double val, long count);
    }

    [PublicAPI]
    public class Histogram
        : IHistogram
    {
        public Histogram(string name, string description)
        {
            Name = name;
            Description = description;
        }

        public string Name { get; }

        public string Description { get; }

        public void Observe(double val) => Observe(val,1);

        public void Observe(double val, long count)
        {
            
        }
    }
}