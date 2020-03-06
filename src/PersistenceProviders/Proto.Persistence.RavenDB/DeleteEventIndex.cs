using System.Linq;
using Raven.Client.Documents.Indexes;

namespace Proto.Persistence.RavenDB
{
    class DeleteEventIndex : AbstractIndexCreationTask<Event>
    {
        public DeleteEventIndex()
        {
            Map = docs => docs.Select(doc => new {doc.ActorName, doc.Index});

            Index(x => x.ActorName, FieldIndexing.Default);
            Index(x => x.Index, FieldIndexing.Default);
        }
    }
}