// -----------------------------------------------------------------------
//   <copyright file="WeightedMemberStatus.cs" company="Asynkron HB">
//       Copyright (C) 2015-2018 Asynkron HB All rights reserved
//   </copyright>
// -----------------------------------------------------------------------

namespace Proto.Cluster.WeightedMemberStrategy
{
    public class WeightedMemberStatusValue : IMemberStatusValue
    {
        public static readonly WeightedMemberStatusValue Default = new WeightedMemberStatusValue { Weight = 5 };

        public int Weight { get; set; }

        public bool IsSame(IMemberStatusValue val) => Weight == (val as WeightedMemberStatusValue)?.Weight;
    }

    public class WeightedMemberStatusValueSerializer : IMemberStatusValueSerializer
    {
        public string Serialize(IMemberStatusValue val)
        {
            var dVal = (WeightedMemberStatusValue)val;
            return dVal.Weight.ToString();
        }

        public IMemberStatusValue Deserialize(string val) 
            => new WeightedMemberStatusValue { Weight = int.Parse(val) };
    }
}