using System;
using System.Threading.Tasks;
using Google.Protobuf;

namespace Proto.Persistence
{
    public interface IProviderState
    {
        void GetEvents(string actorName, int eventIndexStart, Action<object> callback);
        Tuple<object, int> GetSnapshot(string actorName);
        int GetSnapshotInterval();
        void PersistEvent(string actorName, int eventIndex, IMessage @event);
        void PersistSnapshot(string actorName, int eventIndex, IMessage snapshot);
        void Restart();
    }
}