using System;
using System.Threading.Tasks;
using System.Linq;
using Newtonsoft.Json;

namespace Proto.Persistence.Sqlite
{
    public class SqliteProvider : IProvider
    {
        private readonly string _datasource;

        public SqliteProvider(string datasource = "states.db")
        {
            _datasource = datasource;
            try
            {
                using (var db = new SqlitePersistenceContext(_datasource))
                {
                    db.Database.EnsureCreated();
                }
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }

        public async Task DeleteEventsAsync(string actorName, long inclusiveToIndex)
        {
            try
            {
                using (var db = new SqlitePersistenceContext(_datasource))
                {
                    var items = db.Events
                                    .Where(x => x.ActorName == actorName)
                                    .Where(x => x.EventIndex <= inclusiveToIndex)
                                    .ToList();

                    db.RemoveRange(items);

                    await db.SaveChangesAsync();
                }
            }
            catch(Exception ex)
            {
                throw ex;
            }
        }

        public async Task DeleteSnapshotsAsync(string actorName, long inclusiveToIndex)
        {
            try
            {
                using (var db = new SqlitePersistenceContext(_datasource))
                {
                    var items = db.Snapshots
                                    .Where(x => x.ActorName == actorName)
                                    .Where(x => x.SnapshotIndex <= inclusiveToIndex)
                                    .ToList();

                    db.RemoveRange(items);

                    await db.SaveChangesAsync();
                }
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }

        public Task GetEventsAsync(string actorName, long indexStart, long indexEnd, Action<object> callback)
        {
            try
            {
                using (var db = new SqlitePersistenceContext(_datasource))
                {
                    var items = db.Events
                        .Where(x => x.ActorName == actorName)
                        .Where(x => x.EventIndex >= indexStart && x.EventIndex <= indexEnd)
                        .ToList();

                    foreach (var item in items)
                    {
                        callback(JsonConvert.DeserializeObject<object>(item.EventData, new JsonSerializerSettings { TypeNameHandling = TypeNameHandling.Auto }));
                    }
                }
            }
            catch (Exception ex)
            {
                throw ex;
            }

            return Task.FromResult(0);
        }

        public Task<(object Snapshot, long Index)> GetSnapshotAsync(string actorName)
        {
            object snapshot = null;
            long index = 0;

            try
            {
                using (var db = new SqlitePersistenceContext(_datasource))
                {
                    var item = db.Snapshots
                                    .Where(x => x.ActorName == actorName)
                                    .OrderByDescending(x => x.SnapshotIndex)
                                    .FirstOrDefault();

                    if (item != null)
                    {
                        snapshot = JsonConvert.DeserializeObject<object>(item.SnapshotData, new JsonSerializerSettings { TypeNameHandling = TypeNameHandling.Auto });
                        index = item.SnapshotIndex;
                    }
                }
            }
            catch (Exception ex)
            {
                throw ex;
            }

            return Task.FromResult((snapshot, index));
        }

        public async Task PersistEventAsync(string actorName, long index, object @event)
        {
            try
            {
                using (var db = new SqlitePersistenceContext(_datasource))
                {
                    var item = new Event(actorName, index, JsonConvert.SerializeObject(@event, new JsonSerializerSettings { TypeNameHandling = TypeNameHandling.All }));

                    await db.Events.AddAsync(item);

                    await db.SaveChangesAsync();
                }
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }

        public async Task PersistSnapshotAsync(string actorName, long index, object snapshot)
        {
            try
            {
                using (var db = new SqlitePersistenceContext(_datasource))
                {
                    var item = new Snapshot(actorName, index, JsonConvert.SerializeObject(snapshot, new JsonSerializerSettings { TypeNameHandling = TypeNameHandling.All }));

                    await db.Snapshots.AddAsync(item);

                    await db.SaveChangesAsync();
                }
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }
    }
}
