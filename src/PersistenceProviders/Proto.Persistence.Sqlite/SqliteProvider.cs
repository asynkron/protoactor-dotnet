using System;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Microsoft.Data.Sqlite;
using System.Collections.Generic;
using System.Linq;

namespace Proto.Persistence.Sqlite
{
    public class SqliteProvider : IProvider
    {
        private readonly SqliteConnectionStringBuilder _connectionStringBuilder;
        private string _connectionString => $"{_connectionStringBuilder}";

        public SqliteProvider(SqliteConnectionStringBuilder connectionStringBuilder)
        {
            _connectionStringBuilder = connectionStringBuilder;

            try
            {
                using (var connection = new SqliteConnection(_connectionString))
                {
                    connection.Open();

                    var initEventsCommand = connection.CreateCommand();
                    initEventsCommand.CommandText = "CREATE TABLE IF NOT EXISTS Events (Id TEXT, ActorName TEXT, EventIndex REAL, EventData TEXT)";
                    initEventsCommand.ExecuteNonQuery();

                    var initSnapshotsCommand = connection.CreateCommand();
                    initSnapshotsCommand.CommandText = "CREATE TABLE IF NOT EXISTS Snapshots (Id TEXT, ActorName TEXT, SnapshotIndex REAL, SnapshotData TEXT)";
                    initSnapshotsCommand.ExecuteNonQuery();
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
                using (var connection = new SqliteConnection(_connectionString))
                {
                    await connection.OpenAsync();

                    var deleteCommand = connection.CreateCommand();
                    deleteCommand.CommandText = "DELETE FROM Events WHERE ActorName = $actorName AND EventIndex <= $inclusiveToIndex";
                    deleteCommand.Parameters.AddWithValue("$actorName", actorName);
                    deleteCommand.Parameters.AddWithValue("$inclusiveToIndex", inclusiveToIndex);
                    await deleteCommand.ExecuteNonQueryAsync();
                }
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }

        public async Task DeleteSnapshotsAsync(string actorName, long inclusiveToIndex)
        {
            try
            {
                using (var connection = new SqliteConnection(_connectionString))
                {
                    await connection.OpenAsync();

                    var deleteCommand = connection.CreateCommand();
                    deleteCommand.CommandText = "DELETE FROM Snapshots WHERE ActorName = $actorName AND SnapshotIndex <= $inclusiveToIndex";
                    deleteCommand.Parameters.AddWithValue("$actorName", actorName);
                    deleteCommand.Parameters.AddWithValue("$inclusiveToIndex", inclusiveToIndex);
                    await deleteCommand.ExecuteNonQueryAsync();
                }
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }

        public async Task<long> GetEventsAsync(string actorName, long indexStart, long indexEnd, Action<object> callback)
        {
            try
            {
                using (var connection = new SqliteConnection(_connectionString))
                {
                    await connection.OpenAsync();
                    
                    var selectCommand = connection.CreateCommand();
                    selectCommand.CommandText = "SELECT EventIndex, EventData FROM Events WHERE ActorName = $ActorName AND EventIndex >= $IndexStart AND EventIndex <= $IndexEnd ORDER BY EventIndex ASC";
                    selectCommand.Parameters.AddWithValue("$ActorName", actorName);
                    selectCommand.Parameters.AddWithValue("$IndexStart", indexStart);
                    selectCommand.Parameters.AddWithValue("$IndexEnd", indexEnd);

                    var indexes = new List<long>();

                    using (var reader = await selectCommand.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            indexes.Add(Convert.ToInt64(reader["EventIndex"]));

                            callback(JsonConvert.DeserializeObject<object>(reader["EventData"].ToString(), new JsonSerializerSettings { TypeNameHandling = TypeNameHandling.Auto }));
                        }
                    }
                    
                    return indexes.Any() ? indexes.LastOrDefault() : -1;
                }
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }

        public async Task<(object Snapshot, long Index)> GetSnapshotAsync(string actorName)
        {
            object snapshot = null;
            long index = 0;

            try
            {
                using (var connection = new SqliteConnection(_connectionString))
                {
                    await connection.OpenAsync();

                    var selectCommand = connection.CreateCommand();
                    selectCommand.CommandText = "SELECT SnapshotIndex, SnapshotData FROM Snapshots WHERE ActorName = $ActorName ORDER BY SnapshotIndex DESC LIMIT 1";
                    selectCommand.Parameters.AddWithValue("$ActorName", actorName);

                    using (var reader = await selectCommand.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            snapshot = JsonConvert.DeserializeObject<object>(reader["SnapshotData"].ToString(), new JsonSerializerSettings { TypeNameHandling = TypeNameHandling.Auto });
                            index = Convert.ToInt64(reader["SnapshotIndex"]);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                throw ex;
            }

            return (snapshot, index);
        }

        public async Task<long> PersistEventAsync(string actorName, long index, object @event)
        {
            try
            {
                var item = new Event(actorName, index, JsonConvert.SerializeObject(@event, new JsonSerializerSettings { TypeNameHandling = TypeNameHandling.All }));

                using (var connection = new SqliteConnection(_connectionString))
                {
                    await connection.OpenAsync();

                    var insertCommand = connection.CreateCommand();
                    insertCommand.CommandText = "INSERT INTO Events (Id, ActorName, EventIndex, EventData) VALUES ($Id, $ActorName, $EventIndex, $EventData)";
                    insertCommand.Parameters.AddWithValue("$Id", item.Id);
                    insertCommand.Parameters.AddWithValue("$ActorName", item.ActorName);
                    insertCommand.Parameters.AddWithValue("$EventIndex", item.EventIndex);
                    insertCommand.Parameters.AddWithValue("$EventData", item.EventData);
                    await insertCommand.ExecuteNonQueryAsync();

                    return index++;
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
                var item = new Snapshot(actorName, index, JsonConvert.SerializeObject(snapshot, new JsonSerializerSettings { TypeNameHandling = TypeNameHandling.All }));

                using (var connection = new SqliteConnection(_connectionString))
                {
                    await connection.OpenAsync();

                    var insertCommand = connection.CreateCommand();
                    insertCommand.CommandText = "INSERT INTO Snapshots (Id, ActorName, SnapshotIndex, SnapshotData) VALUES ($Id, $ActorName, $SnapshotIndex, $SnapshotData)";
                    insertCommand.Parameters.AddWithValue("$Id", item.Id);
                    insertCommand.Parameters.AddWithValue("$ActorName", item.ActorName);
                    insertCommand.Parameters.AddWithValue("$SnapshotIndex", item.SnapshotIndex);
                    insertCommand.Parameters.AddWithValue("$SnapshotData", item.SnapshotData);
                    await insertCommand.ExecuteNonQueryAsync();
                }
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }
    }
}