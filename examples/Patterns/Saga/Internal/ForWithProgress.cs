// -----------------------------------------------------------------------
// <copyright file="ForWithProgress.cs" company="Asynkron AB">
//      Copyright (C) 2015-2020 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------
using System;

namespace Saga.Internal
{
    public class ForWithProgress
    {
        private readonly int _everyNth;
        private readonly bool _runBothOnEvery;
        private readonly bool _runOnStart;
        private readonly int _total;

        public ForWithProgress(int total, int everyNth, bool runBothOnEvery, bool runOnStart)
        {
            _runBothOnEvery = runBothOnEvery;
            _runOnStart = runOnStart;
            _total = total;
            _everyNth = everyNth;
        }

        public void EveryNth(Action<int> everyNthAction, Action<int, bool> everyAction)
        {
            for (var i = 1; i < _total + 1; i++)
            {
                var must = MustRunNth(i);
                if (must) everyNthAction(i);
                if (must && !_runBothOnEvery) continue;

                everyAction(i, must);
            }
            
            bool MustRunNth(int current) => current switch
            {
                0 when _runOnStart => true,
                0                  => false,
                _                  => current % _everyNth == 0
            };
        }
    }
}