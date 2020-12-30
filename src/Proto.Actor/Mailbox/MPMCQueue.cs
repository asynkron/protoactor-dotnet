// -----------------------------------------------------------------------
// <copyright file="MPMCQueue.cs" company="Asynkron AB">
//      Copyright (C) 2015-2020 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------
using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace Proto.Mailbox
{
    [StructLayout(LayoutKind.Explicit, Size = 192, CharSet = CharSet.Ansi)]
    public class MPMCQueue
    {
        [FieldOffset(0)] private readonly Cell[] _buffer;

        [FieldOffset(8)] private readonly int _bufferMask;

        [FieldOffset(128)] private int _dequeuePos;

        [FieldOffset(64)] private int _enqueuePos;

        public MPMCQueue(int bufferSize)
        {
            if (bufferSize < 2) throw new ArgumentException($"{nameof(bufferSize)} should be greater than 2");

            if ((bufferSize & (bufferSize - 1)) != 0)
                throw new ArgumentException($"{nameof(bufferSize)} should be a power of 2");

            _bufferMask = bufferSize - 1;
            _buffer = new Cell[bufferSize];

            for (var i = 0; i < bufferSize; i++)
            {
                _buffer[i] = new Cell(i, null);
            }

            _enqueuePos = 0;
            _dequeuePos = 0;
        }

        public int Count => _enqueuePos - _dequeuePos;

        private bool TryEnqueue(object item)
        {
            do
            {
                var buffer = _buffer;
                var pos = _enqueuePos;
                var index = pos & _bufferMask;
                var cell = buffer[index];

                if (cell.Sequence == pos && Interlocked.CompareExchange(ref _enqueuePos, pos + 1, pos) == pos)
                {
                    buffer[index].Element = item;
                    Volatile.Write(ref buffer[index].Sequence, pos + 1);
                    return true;
                }

                if (cell.Sequence < pos) return false;
            } while (true);
        }

        public void Enqueue(object item)
        {
            while (true)
            {
                if (TryEnqueue(item)) break;

                Task.Delay(1)
                    .Wait(); // Could be Thread.Sleep(1) or Thread.SpinWait() if the assembly is not portable lib.
            }
        }

        public bool TryDequeue(out object? result)
        {
            do
            {
                var buffer = _buffer;
                var bufferMask = _bufferMask;
                var pos = _dequeuePos;
                var index = pos & bufferMask;
                var cell = buffer[index];

                if (cell.Sequence == pos + 1 && Interlocked.CompareExchange(ref _dequeuePos, pos + 1, pos) == pos)
                {
                    result = cell.Element;
                    buffer[index].Element = null;
                    Volatile.Write(ref buffer[index].Sequence, pos + bufferMask + 1);
                    return true;
                }

                if (cell.Sequence < pos + 1)
                {
                    result = default;
                    return false;
                }
            } while (true);
        }

        [StructLayout(LayoutKind.Explicit, Size = 16, CharSet = CharSet.Ansi)]
        private struct Cell
        {
            [FieldOffset(0)] public int Sequence;
            [FieldOffset(8)] public object? Element;

            public Cell(int sequence, object? element)
            {
                Sequence = sequence;
                Element = element;
            }
        }
    }
}