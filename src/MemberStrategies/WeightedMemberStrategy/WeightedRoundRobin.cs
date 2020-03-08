// -----------------------------------------------------------------------
//   <copyright file="WeightedMemberStatus.cs" company="Asynkron HB">
//       Copyright (C) 2015-2018 Asynkron HB All rights reserved
//   </copyright>
// -----------------------------------------------------------------------

using System;
using System.Threading;

namespace Proto.Cluster.WeightedMemberStrategy
{
    public class WeightedRoundRobin
    {
        private readonly object _lock = new object();
        private int _currIndex;
        private int _currWeight;
        private int _maxWeight;
        private int _gcd;

        private readonly IMemberStrategy _memberStrategy;

        public WeightedRoundRobin(IMemberStrategy memberStrategy)
        {
            _memberStrategy = memberStrategy;
        }

        public string GetNode()
        {
            var members = _memberStrategy.GetAllMembers();
            var l = members.Count;
            if (l == 0) return "";
            if (l == 1) return members[0].Address;

            lock (_lock)
            {
                while (true)
                {
                    _currIndex = (_currIndex + 1) % l;

                    if (_currIndex == 0)
                    {
                        if (_currWeight > _gcd)
                        {
                            _currWeight -= _gcd;
                        }
                        else
                        {
                            _currWeight = _maxWeight;
                        }
                    }

                    if (((WeightedMemberStatusValue) members[_currIndex].StatusValue).Weight >= _currWeight)
                    {
                        return members[_currIndex].Address;
                    }
                }
            }
        }

        public void UpdateRR()
        {
            _maxWeight = GetMaxWeight();
            _gcd = GetGCD();
        }

        private int GetMaxWeight()
        {
            var max = 0;

            foreach (var m in _memberStrategy.GetAllMembers())
            {
                var statusVal = (WeightedMemberStatusValue) m.StatusValue;

                if (statusVal.Weight > max)
                    max = statusVal.Weight;
            }

            return max;
        }

        private int GetGCD()
        {
            var members = _memberStrategy.GetAllMembers();
            if (members.Count == 0) return 0;

            var ints = new int[members.Count];

            for (int i = 0; i < members.Count; i++)
            {
                ints[i] = ((WeightedMemberStatusValue) members[i].StatusValue).Weight;
            }

            return NGCD(ints);
        }

        private static int GCD(int a, int b)
        {
            while (true)
            {
                if (a < b)
                {
                    var c = a;
                    a = b;
                    b = c;
                }

                if (b == 0) return a;

                var a1 = a;
                a = b;
                b = a1 % b;
            }
        }

        private static int NGCD(int[] ints)
        {
            var l = ints.Length;
            return l == 1 ? ints[0] : GCD(ints[l - 1], NGCD(SubArray(ints, 0, l - 1)));
        }

        private static T[] SubArray<T>(T[] data, int index, int length)
        {
            var result = new T[length];
            Array.Copy(data, index, result, 0, length);
            return result;
        }
    }
}