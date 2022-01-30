// -----------------------------------------------------------------------
// <copyright file="AgentServiceRegistration.cs" company="Asynkron AB">
//      Copyright (C) 2015-2022 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------
namespace Proto.Cluster.Testing
{
    public class AgentServiceRegistration
    {
        public string ID { get; set; }
        public string Host { get; set; }
        public int Port { get; set; }
        public string[] Kinds { get; set; }
    }
}