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
            bool mustRunNth(int current)
            {
                if (current == 0 && _runOnStart) return true;
                if (current == 0) return false;
                return current % _everyNth == 0;
            }

            for (var i = 1; i < _total + 1; i++)
            {
                var must = mustRunNth(i);
                if (must) everyNthAction(i);
                if (must && !_runBothOnEvery) continue;
                everyAction(i, must);
            }
        }
    }
}