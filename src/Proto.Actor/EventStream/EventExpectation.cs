// -----------------------------------------------------------------------
// <copyright file="EventExpectation.cs" company="Asynkron AB">
//      Copyright (C) 2015-2020 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------
using System;
using System.Threading.Tasks;
using JetBrains.Annotations;

// ReSharper disable once CheckNamespace
namespace Proto
{
    [PublicAPI]
    class EventExpectation<T>
    {
        private readonly Func<T, bool> _predicate;

        private readonly TaskCompletionSource<T> _source =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public EventExpectation(Func<T, bool> predicate) => _predicate = predicate;

        public Task<T> Task => _source.Task;

        public bool Evaluate(T @event)
        {
            if (!_predicate(@event)) return false;

            _source.SetResult(@event);
            return true;
        }
    }
}