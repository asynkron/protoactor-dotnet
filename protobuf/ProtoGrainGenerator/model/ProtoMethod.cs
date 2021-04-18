// -----------------------------------------------------------------------
// <copyright file="Proto.cs" company="Asynkron AB">
//      Copyright (C) 2015-2020 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------
namespace ProtoBuf
{
    public class ProtoMethod
    {
        public int Index { get; set; }
        public string Name { get; set; } = null!;
        public string InputName { get; set; } = null!;
        public string OutputName { get; set; } = null!;

        public string InputNameFull { get; set; } = null!;
        
        public string OutputNameFull { get; set; } = null!;
    }
}