// -----------------------------------------------------------------------
// <copyright file="PidLookupEntity.cs" company="Asynkron AB">
//      Copyright (C) 2015-2020 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------
using MongoDB.Bson.Serialization.Attributes;

namespace Proto.Cluster.Identity.MongoDb
{
    public class PidLookupEntity
    {
        [BsonId] public string Key { get; set; } = null!;
        public string Identity { get; set; } = null!;
        public string? UniqueIdentity { get; set; }
        public string Kind { get; set; } = null!;
        public string? Address { get; set; }
        public string? MemberId { get; set; }
        public string? LockedBy { get; set; }
        public int Revision { get; set; }
    }
}