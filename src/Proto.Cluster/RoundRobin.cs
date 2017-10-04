namespace Proto.Cluster
{
    internal class RoundRobin
    {
        private int currIndex;
        private int currWeight;
        private int maxWeight;
        private int gcd;

        private MemberNodeSet m;

        internal RoundRobin(MemberNodeSet m)
        {
            this.m = m;
        }

        internal string GetNode()
        {
            var l = m.nodes.Count;
            if (l == 0) return "";
            if (l == 1) return m.nodes[0].Name;

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
                if (m.nodes[currIndex].Weight >= currWeight)
                {
                    return m.nodes[currIndex].Name;
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
            foreach (var n in m.nodes)
            {
                if (n.Weight > max)
                    max = n.Weight;
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