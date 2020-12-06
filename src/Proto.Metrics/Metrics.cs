// -----------------------------------------------------------------------
// <copyright file="Metrics.cs" company="Asynkron AB">
//      Copyright (C) 2015-2020 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------
using System;
using JetBrains.Annotations;
using Proto.Extensions;

namespace Proto.Metrics
{
    [PublicAPI]
    public class Metrics : IActorSystemExtension<Metrics>
    {
        public ICounter CreateCounter(string name, string description) => new Counter(name, description);

        public IGauge CreateGauge(string name, string description) => new Gauge(name, description);
    }
}