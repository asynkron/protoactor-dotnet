// -----------------------------------------------------------------------
// <copyright file="Behavior.cs" company="Asynkron AB">
//      Copyright (C) 2015-2020 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Proto
{
    public class Behavior
    {
        private readonly Stack<Receive> _behaviors = new();

        public Behavior()
        {
        }

        public Behavior(Receive receive) => Become(receive);

        public void Become(Receive receive)
        {
            _behaviors.Clear();
            _behaviors.Push(receive);
        }

        public void BecomeStacked(Receive receive) => _behaviors.Push(receive);

        public void UnbecomeStacked() => _behaviors.Pop();

        public Task ReceiveAsync(IContext context)
        {
            var behavior = _behaviors.Peek();
            return behavior(context);
        }
    }
}