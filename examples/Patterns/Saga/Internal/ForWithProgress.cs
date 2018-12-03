using System;

namespace Saga.Internal
{
    public class ForWithProgress
    {
        private bool _runBothOnEvery;
        private bool _runOnStart;
        private int _total;
        private int _everyNth;
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
                return (current % _everyNth == 0);
            }
            for (int i = 1; i < _total + 1; i++)
            {
                bool must = mustRunNth(i);
                if (must) everyNthAction(i);
                if (must && !_runBothOnEvery) continue;
                everyAction(i, must);
            }
        }
    }
}
