using System;

namespace Proto.Cluster.WeightedMemberStrategy
{
    public class WeightedRoundRobin
    {
        private int currIndex;
        private int currWeight;
        private int maxWeight;
        private int gcd;

        private IMemberStrategy m;

        public WeightedRoundRobin(IMemberStrategy m)
        {
            this.m = m;
        }

        public string GetNode()
        {
            var members = m.GetAllMembers();
            var l = members.Count;
            if (l == 0) return "";
            if (l == 1) return members[0].Address;

            while (true)
            {
                currIndex = (currIndex + 1) % l;
                if (currIndex == 0)
                {
                    currWeight = currWeight - gcd;
                    if (currWeight <= 0)
                    {
                        currWeight = maxWeight;
                    }
                }
                if (((WeightedMemberStatusValue) members[currIndex].StatusValue).Weight >= currWeight)
                {
                    return members[currIndex].Address;
                }
            }
        }

        public void UpdateRR()
        {
            maxWeight = GetMaxWeight();
            gcd = GetGCD();
        }

        private int GetMaxWeight()
        {
            var max = 0;
            foreach (var m in m.GetAllMembers())
            {
                var statusVal = (WeightedMemberStatusValue) m.StatusValue;
                if (statusVal.Weight > max)
                    max = statusVal.Weight;
            }
            return max;
        }

        private int GetGCD()
        {
            var members = m.GetAllMembers();
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
            if (a < b)
            {
                var c = a;
                a = b;
                b = c;
            }
            if (b == 0) return a;
            return GCD(b, a % b);
        }

        private static int NGCD(int[] ints)
        {
            var l = ints.Length;
            if (l == 1)
                return ints[0];
            return GCD(ints[l - 1], NGCD(SubArray(ints, 0, l - 1)));
        }

        private static T[] SubArray<T>(T[] data, int index, int length)
        {
            T[] result = new T[length];
            Array.Copy(data, index, result, 0, length);
            return result;
        }
    }
}