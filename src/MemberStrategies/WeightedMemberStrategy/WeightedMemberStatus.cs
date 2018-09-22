// -----------------------------------------------------------------------
//   <copyright file="WeightedMemberStatus.cs" company="Asynkron HB">
//       Copyright (C) 2015-2018 Asynkron HB All rights reserved
//   </copyright>
// -----------------------------------------------------------------------

using System.Text;

namespace Proto.Cluster.WeightedMemberStrategy
{
    public class WeightedMemberStatusValue : IMemberStatusValue
    {
        public static readonly WeightedMemberStatusValue Default = new WeightedMemberStatusValue {Weight = 5};

        public int Weight { get; set; }

        public bool IsSame(IMemberStatusValue val)
        {
            return Weight == (val as WeightedMemberStatusValue)?.Weight;
        }
    }

    public class WeightedMemberStatusValueSerializer : IMemberStatusValueSerializer
    {
        public byte[] ToValueBytes(IMemberStatusValue val)
        {
            var dVal = (WeightedMemberStatusValue) val;
            return Encoding.UTF8.GetBytes(dVal.Weight.ToString());
        }

        public IMemberStatusValue FromValueBytes(byte[] val)
        {
            var weight =  Encoding.UTF8.GetString(val);
            return new WeightedMemberStatusValue
            {
                Weight = int.Parse(weight)
            };
        }
    }
}