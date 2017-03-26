using System.Linq;
using Raven.Abstractions.Indexing;
using Raven.Client.Indexes;

namespace Proto.Persistence.RavenDB
{
    internal class DeleteSnapshotIndex : AbstractIndexCreationTask<Snapshot>
    {
        public DeleteSnapshotIndex()
        {
            Map = docs => from doc in docs
                          select new
                          {
                              ActorName = doc.ActorName,
                              Index = doc.Index
                          };

            Index(x => x.ActorName, FieldIndexing.Default);
            Sort(x => x.ActorName, SortOptions.String);

            Index(x => x.Index, FieldIndexing.Default);
            Sort(x => x.Index, SortOptions.Long);
        }
    }
}
