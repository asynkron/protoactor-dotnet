using System.Linq;
using Raven.Client.Documents.Indexes;

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
            Index(x => x.Index, FieldIndexing.Default);
        }
    }
}