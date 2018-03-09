using System.Linq;
#if NET452
using Raven.Abstractions.Indexing;
using Raven.Client.Indexes;
#else
using Raven.Client.Documents.Indexes;
#endif

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
#if NET452
            Sort(x => x.ActorName, SortOptions.String);
#endif
            Index(x => x.Index, FieldIndexing.Default);
#if NET452
            Sort(x => x.Index, SortOptions.Long);
#endif
        }
    }
}