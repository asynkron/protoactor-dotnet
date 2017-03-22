using System.Linq;
using Raven.Abstractions.Indexing;
using Raven.Client.Indexes;

namespace Proto.Persistence.RavenDB
{
    internal class DeleteEnvelopeSnapshotIndex : AbstractIndexCreationTask<Envelope>
    {
        public DeleteEnvelopeSnapshotIndex()
        {
            Map = docs => from doc in docs
                          select new
                          {
                              ActorName = doc.ActorName,
                              Type = "snapshot",
                              EventIndex = doc.EventIndex
                          };

            Index(x => x.ActorName, FieldIndexing.Default);
            Sort(x => x.ActorName, SortOptions.String);

            Index(x => x.Type, FieldIndexing.Default);
            Sort(x => x.Type, SortOptions.String);

            Index(x => x.EventIndex, FieldIndexing.Default);
            Sort(x => x.EventIndex, SortOptions.Long);
        }
    }

    internal class DeleteEnvelopeEventIndex : AbstractIndexCreationTask<Envelope>
    {
        public DeleteEnvelopeEventIndex()
        {
            Map = docs => from doc in docs
                          select new
                          {
                              ActorName = doc.ActorName,
                              Type = "event",
                              EventIndex = doc.EventIndex
                          };

            Index(x => x.ActorName, FieldIndexing.Default);
            Sort(x => x.ActorName, SortOptions.String);

            Index(x => x.Type, FieldIndexing.Default);
            Sort(x => x.Type, SortOptions.String);

            Index(x => x.EventIndex, FieldIndexing.Default);
            Sort(x => x.EventIndex, SortOptions.Long);
        }
    }
}
