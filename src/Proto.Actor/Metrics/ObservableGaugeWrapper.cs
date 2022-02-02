// -----------------------------------------------------------------------
// <copyright file="ObservableGaugeWrapper.cs" company="Asynkron AB">
//      Copyright (C) 2015-2022 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics.Metrics;
using System.Linq;

namespace Proto.Metrics
{
    public class ObservableGaugeWrapper<T> where T : struct
    {
        private ImmutableList<Func<IEnumerable<Measurement<T>>>> _observers = ImmutableList<Func<IEnumerable<Measurement<T>>>>.Empty;

        public void AddObserver(Func<IEnumerable<Measurement<T>>> observer) => _observers = _observers.Add(observer);

        public void RemoveObserver(Func<IEnumerable<Measurement<T>>> observer) => _observers = _observers.Remove(observer);

        public IEnumerable<Measurement<T>> Observe() => _observers.SelectMany(o => o());
    }
}