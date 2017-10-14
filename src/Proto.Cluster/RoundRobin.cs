namespace Proto.Cluster
{
    internal class SimpleRoundRobin
    {
        private int val;

        private IMemberStrategy m;

        internal SimpleRoundRobin(IMemberStrategy m)
        {
            this.m = m;
        }

        internal string GetNode()
        {
            var members = m.GetAllMembers();
            var l = members.Count;
            if (l == 0) return "";
            if (l == 1) return members[0].Address;

            return members[val++ % l].Address;
        }
    }

    internal class WeightedRoundRobin
    {
        private int currIndex;
        private int currWeight;
        private int maxWeight;
        private int gcd;

        private IMemberStrategy m;

        internal WeightedRoundRobin(IMemberStrategy m)
        {
            this.m = m;
        }

        internal string GetNode()
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
                    currWeight = currWeight - 1;
                    if (currWeight <= 0)
                    {
                        currWeight = maxWeight;
                    }
                }
                if (((MemberStatusValue) members[currIndex].StatusValue).Weight >= currWeight)
                {
                    return members[currIndex].Address;
                }
            }
        }

        internal void UpdateRR()
        {
            maxWeight = GetMaxWeight();
            //gcd = GetGCD();
        }

        private int GetMaxWeight()
        {
            var max = 0;
            foreach (var m in m.GetAllMembers())
            {
                var statusVal = (MemberStatusValue) m.StatusValue;
                if (statusVal.Weight > max)
                    max = statusVal.Weight;
            }
            return max;
        }

        /*
        private int GetGCD()
        {
            if (m.nodes.Count == 0) return 0;

            var ints = new int[m.nodes.Count];
            for (int i = 0; i < m.nodes.Count; i++)
            {
                ints[i] = m.nodes[i].Weight;
            }
            return
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
        */
    }
}