using System;
using System.Collections.Generic;
using System.Text;

namespace Proto
{

    public interface IMailboxQueue
    {
        bool HasMessages { get; }
        void Push(object message);
        object Pop();
    }
}
