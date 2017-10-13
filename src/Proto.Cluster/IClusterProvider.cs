// -----------------------------------------------------------------------
//   <copyright file="IClusterProvider.cs" company="Asynkron HB">
//       Copyright (C) 2015-2017 Asynkron HB All rights reserved
//   </copyright>
// -----------------------------------------------------------------------

using System.Threading.Tasks;

namespace Proto.Cluster
{
	public interface IClusterProvider
	{
		Task RegisterMemberAsync(string clusterName, string h, int p, int weight, string[] kinds);
		void MonitorMemberStatusChanges();
		Task UpdateWeight(int weight);
		Task DeregisterMemberAsync();
		Task Shutdown();
	}
}