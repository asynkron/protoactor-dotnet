using System.Linq;
using Raven.Client.Documents.Indexes;

namespace Proto.Persistence.RavenDB
{
    internal class DeleteEventIndex : AbstractIndexCreationTask<Event>
    {
        public DeleteEventIndex()
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