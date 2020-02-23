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
        private string ConnectionString => $"{_connectionStringBuilder}";

        private static readonly JsonSerializerSettings AutoTypeSettings = new JsonSerializerSettings {TypeNameHandling = TypeNameHandling.Auto};
        private static readonly JsonSerializerSettings AllTypeSettings = new JsonSerializerSettings {TypeNameHandling = TypeNameHandling.All};

        public SqliteProvider(SqliteConnectionStringBuilder connectionStringBuilder)
        {
            _connectionStringBuilder = connectionStringBuilder;

            using var connection = new SqliteConnection(ConnectionString);

            connection.Open();

            using var initEventsCommand = connection.CreateCommand();
            
            initEventsCommand.CommandText = "CREATE TABLE IF NOT EXISTS Events (Id TEXT, ActorName TEXT, EventIndex REAL, EventData TEXT)";
            initEventsCommand.ExecuteNonQuery();

            using var initSnapshotsCommand = connection.CreateCommand();

            initSnapshotsCommand.CommandText =
                "CREATE TABLE IF NOT EXISTS Snapshots (Id TEXT, ActorName TEXT, SnapshotIndex REAL, SnapshotData TEXT)";
            initSnapshotsCommand.ExecuteNonQuery();
        }

        public async Task DeleteEventsAsync(string actorName, long inclusiveToIndex)
        {
            using var connection = new SqliteConnection(ConnectionString);

            await connection.OpenAsync();

            using var deleteCommand = CreateCommand(
                connection,
                "DELETE FROM Events WHERE ActorName = $actorName AND EventIndex <= $inclusiveToIndex",
                ("$actorName", actorName),
                ("$inclusiveToIndex", inclusiveToIndex)
            );

            await deleteCommand.ExecuteNonQueryAsync();
        }

        public async Task DeleteSnapshotsAsync(string actorName, long inclusiveToIndex)
        {
            using var connection = new SqliteConnection(ConnectionString);

            await connection.OpenAsync();

            using var deleteCommand = CreateCommand(
                connection,
                "DELETE FROM Snapshots WHERE ActorName = $actorName AND SnapshotIndex <= $inclusiveToIndex",
                ("$actorName", actorName),
                ("$inclusiveToIndex", inclusiveToIndex)
            );

            await deleteCommand.ExecuteNonQueryAsync();
        }

        public async Task<long> GetEventsAsync(string actorName, long indexStart, long indexEnd, Action<object> callback)
        {
            using var connection = new SqliteConnection(ConnectionString);

            await connection.OpenAsync();

            using var selectCommand = CreateCommand(
                connection,
                "SELECT EventIndex, EventData FROM Events WHERE ActorName = $ActorName AND EventIndex >= $IndexStart AND EventIndex <= $IndexEnd ORDER BY EventIndex ASC",
                ("$ActorName", actorName),
                ("$IndexStart", indexStart),
                ("$IndexEnd", indexEnd)
            );

            var indexes = new List<long>();

            using var reader = await selectCommand.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                indexes.Add(Convert.ToInt64(reader["EventIndex"]));

                callback(JsonConvert.DeserializeObject<object>(reader["EventData"].ToString(), AutoTypeSettings));
            }

            return indexes.Any() ? indexes.LastOrDefault() : -1;
        }

        public async Task<(object Snapshot, long Index)> GetSnapshotAsync(string actorName)
        {
            object snapshot = null;
            long index = 0;

            using var connection = new SqliteConnection(ConnectionString);

            await connection.OpenAsync();

            using var selectCommand = CreateCommand(
                connection,
                "SELECT SnapshotIndex, SnapshotData FROM Snapshots WHERE ActorName = $ActorName ORDER BY SnapshotIndex DESC LIMIT 1",
                ("$ActorName", actorName)
            );

            using var reader = await selectCommand.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                snapshot = JsonConvert.DeserializeObject<object>(reader["SnapshotData"].ToString(), AutoTypeSettings);
                index = Convert.ToInt64(reader["SnapshotIndex"]);
            }

            return (snapshot, index);
        }

        public async Task<long> PersistEventAsync(string actorName, long index, object @event)
        {
            var item = new Event(
                actorName, index, JsonConvert.SerializeObject(@event, AllTypeSettings)
            );

            using var connection = new SqliteConnection(ConnectionString);

            await connection.OpenAsync();

            using var insertCommand = CreateCommand(
                connection,
                "INSERT INTO Events (Id, ActorName, EventIndex, EventData) VALUES ($Id, $ActorName, $EventIndex, $EventData)",
                ("$Id", item.Id),
                ("$ActorName", item.ActorName),
                ("$EventIndex", item.EventIndex),
                ("$EventData", item.EventData)
            );

            await insertCommand.ExecuteNonQueryAsync();

            return index++;
        }

        public async Task PersistSnapshotAsync(string actorName, long index, object snapshot)
        {
            var item = new Snapshot(
                actorName, index, JsonConvert.SerializeObject(snapshot, AllTypeSettings)
            );

            using var connection = new SqliteConnection(ConnectionString);

            await connection.OpenAsync();

            using var insertCommand = CreateCommand(
                connection,
                "INSERT INTO Snapshots (Id, ActorName, SnapshotIndex, SnapshotData) VALUES ($Id, $ActorName, $SnapshotIndex, $SnapshotData)",
                ("$Id", item.Id),
                ("$ActorName", item.ActorName),
                ("$SnapshotIndex", item.SnapshotIndex),
                ("$SnapshotData", item.SnapshotData)
            );

            await insertCommand.ExecuteNonQueryAsync();
        }

        private static SqliteCommand CreateCommand(SqliteConnection connection, string command, params (string Name, object Value)[] parameters)
        {
            var sqliteCommand = connection.CreateCommand();
            sqliteCommand.CommandText = command;
            sqliteCommand.Parameters.AddRange(parameters.Select(x => new SqliteParameter(x.Name, x.Value)));
            return sqliteCommand;
        }
    }
}