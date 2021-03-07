// -----------------------------------------------------------------------
// <copyright file="ProducerBatch.cs" company="Asynkron AB">
//      Copyright (C) 2015-2021 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Proto.Cluster
{
    public partial class ProducerBatch
    {
        public List<TaskCompletionSource<bool>> DeliveryReports { get; } = new();
    }
}