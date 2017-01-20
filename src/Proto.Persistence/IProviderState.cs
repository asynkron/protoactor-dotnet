using System;
using System.Threading.Tasks;
using Google.Protobuf;

namespace Proto.Persistence
{
    public interface IProviderState
    {
        void Restart();
        int GetSnapshotInterval();
        Tuple<object, int> GetSnapshot(string actorName);
        void GetEvents(string actorName, int eventIndexStart, Action<object> callback);
        void PersistEvent(string actorName, int eventIndex, IMessage @event);
        void PersistSnapshot(string actorName, int eventIndex, IMessage snapshot);
    }
}