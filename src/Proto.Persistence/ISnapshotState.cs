using System.Threading.Tasks;

namespace Proto.Persistence
{
    public interface ISnapshotState
    {
        Task<(object Snapshot, long Index)> GetSnapshotAsync(string actorName);
        Task PersistSnapshotAsync(string actorName, long index, object snapshot);
        Task DeleteSnapshotsAsync(string actorName, long inclusiveToIndex);
    }
}