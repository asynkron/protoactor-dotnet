using System;
using System.Threading.Tasks;
using System.Data.SqlClient;
using System.Collections.Generic;
using System.Data;
using Newtonsoft.Json;
using static System.Data.SqlDbType;

namespace Proto.Persistence.SqlServer
{
    public class SqlServerProvider : IProvider
    {
        private readonly string _connectionString;
        private readonly string _tableSnapshots;
        private readonly string _tableEvents;
        private readonly string _tableSchema;

        private static readonly JsonSerializerSettings AutoTypeSettings = new JsonSerializerSettings {TypeNameHandling = TypeNameHandling.Auto};
        private static readonly JsonSerializerSettings AllTypeSettings = new JsonSerializerSettings {TypeNameHandling = TypeNameHandling.All};

        private readonly string _sqlDeleteEvents;
        private readonly string _sqlDeleteSnapshots;
        private readonly string _sqlReadEvents;
        private readonly string _sqlReadSnapshot;
        private readonly string _sqlSaveEvents;
        private readonly string _sqlSaveSnapshot;

        public SqlServerProvider(
            string connectionString, bool autoCreateTables = false, string useTablesWithPrefix = "", string useTablesWithSchema = "dbo"
        )
        {
            _connectionString = connectionString;
            _tableSchema = useTablesWithSchema;
            _tableSnapshots = string.IsNullOrEmpty(useTablesWithPrefix) ? "Snapshots" : $"{useTablesWithPrefix}_Snapshots";
            _tableEvents = string.IsNullOrEmpty(useTablesWithPrefix) ? "Events" : $"{useTablesWithPrefix}_Events";

            if (autoCreateTables)
            {
                CreateSnapshotTable();
                CreateEventTable();
            }

            // execute string interpolation once
            _sqlDeleteEvents = $@"DELETE FROM {_tableSchema}.{_tableEvents} WHERE ActorName = @ActorName AND EventIndex <= @EventIndex";
            _sqlDeleteSnapshots = $@"DELETE FROM {_tableSchema}.{_tableSnapshots} WHERE ActorName = @ActorName AND SnapshotIndex <= @SnapshotIndex";

            _sqlReadEvents =
                $@"SELECT EventIndex, EventData FROM {_tableSchema}.{_tableEvents} WHERE ActorName = @ActorName AND EventIndex >= @IndexStart AND EventIndex <= @IndexEnd ORDER BY EventIndex ASC";

            _sqlReadSnapshot =
                $@"SELECT TOP 1 SnapshotIndex, SnapshotData FROM {_tableSchema}.{_tableSnapshots} WHERE ActorName = @ActorName ORDER BY SnapshotIndex DESC";

            _sqlSaveEvents =
                $@"INSERT INTO {_tableSchema}.{_tableEvents} (Id, ActorName, EventIndex, EventData) VALUES (@Id, @ActorName, @EventIndex, @EventData)";

            _sqlSaveSnapshot =
                $@"INSERT INTO {_tableSchema}.{_tableSnapshots} (Id, ActorName, SnapshotIndex, SnapshotData) VALUES (@Id, @ActorName, @SnapshotIndex, @SnapshotData)";
        }

        public Task DeleteEventsAsync(string actorName, long inclusiveToIndex)
            => ExecuteNonQueryAsync(
                _sqlDeleteEvents,
                CreateParameter("ActorName", NVarChar, actorName),
                CreateParameter("EventIndex", BigInt, inclusiveToIndex)
            );

        public Task DeleteSnapshotsAsync(string actorName, long inclusiveToIndex)
            => ExecuteNonQueryAsync(
                _sqlDeleteSnapshots,
                CreateParameter("ActorName", NVarChar, actorName),
                CreateParameter("SnapshotIndex", BigInt, inclusiveToIndex)
            );

        public async Task<long> GetEventsAsync(string actorName, long indexStart, long indexEnd, Action<object> callback)
        {
            using var connection = new SqlConnection(_connectionString);

            using var command = new SqlCommand(_sqlReadEvents, connection);

            await connection.OpenAsync();

            command.Parameters.AddRange(
                new[]
                {
                    CreateParameter("ActorName", NVarChar, actorName),
                    CreateParameter("IndexStart", BigInt, indexStart),
                    CreateParameter("IndexEnd", BigInt, indexEnd)
                }
            );

            long lastIndex = -1;

            var eventReader = await command.ExecuteReaderAsync();

            while (await eventReader.ReadAsync())
            {
                lastIndex = (long) eventReader["EventIndex"];

                callback(JsonConvert.DeserializeObject<object>(eventReader["EventData"].ToString(), AutoTypeSettings));
            }

            return lastIndex;
        }

        public async Task<(object Snapshot, long Index)> GetSnapshotAsync(string actorName)
        {
            long snapshotIndex = 0;
            object snapshotData = null;

            using var connection = new SqlConnection(_connectionString);

            using var command = new SqlCommand(_sqlReadSnapshot, connection);

            await connection.OpenAsync();

            command.Parameters.Add(CreateParameter("ActorName", NVarChar, actorName));

            var snapshotReader = await command.ExecuteReaderAsync();

            while (await snapshotReader.ReadAsync())
            {
                snapshotIndex = Convert.ToInt64(snapshotReader["SnapshotIndex"]);

                snapshotData = JsonConvert.DeserializeObject<object>(
                    snapshotReader["SnapshotData"].ToString(), AutoTypeSettings
                );
            }

            return (snapshotData, snapshotIndex);
        }

        public async Task<long> PersistEventAsync(string actorName, long index, object @event)
        {
            var item = new Event(actorName, index, @event);

            await ExecuteNonQueryAsync(
                _sqlSaveEvents,
                CreateParameter("Id", NVarChar, item.Id),
                CreateParameter("ActorName", NVarChar, item.ActorName),
                CreateParameter("EventIndex", BigInt, item.EventIndex),
                CreateParameter("EventData", NVarChar, JsonConvert.SerializeObject(item.EventData, AllTypeSettings))
            );

            return index++;
        }

        public Task PersistSnapshotAsync(string actorName, long index, object snapshot)
        {
            var item = new Snapshot(actorName, index, snapshot);

            return ExecuteNonQueryAsync(
                _sqlSaveSnapshot,
                CreateParameter("Id", NVarChar, item.Id),
                CreateParameter("ActorName", NVarChar, item.ActorName),
                CreateParameter("SnapshotIndex", BigInt, item.SnapshotIndex),
                CreateParameter(
                    "SnapshotData", NVarChar, JsonConvert.SerializeObject(item.SnapshotData, AllTypeSettings)
                )
            );
        }

        private void CreateSnapshotTable()
        {
            var sql = $@"
            IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_SCHEMA = '{_tableSchema}' AND TABLE_NAME = '{_tableSnapshots}')
            BEGIN
                CREATE TABLE {_tableSchema}.{_tableSnapshots} (
	                Id NVARCHAR(255) NOT NULL,
                    ActorName NVARCHAR(255) NOT NULL,
	                SnapshotIndex BIGINT NOT NULL,
                    SnapshotData NVARCHAR(MAX) NOT NULL,
                    Created DATETIME NOT NULL CONSTRAINT [DF_{_tableSnapshots}_Created] DEFAULT (getdate()),
                    CONSTRAINT PK_{_tableSnapshots} PRIMARY KEY CLUSTERED (
                    Id ASC
                ));
                CREATE INDEX IX_{_tableSnapshots}_ActorNameAndSnapshotIndex ON {_tableSchema}.{_tableSnapshots}(ActorName ASC, SnapshotIndex ASC);
            END
            ";

            ExecuteNonQuery(sql);
        }

        private void CreateEventTable()
        {
            var sql = $@"
            IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_SCHEMA = '{_tableSchema}' AND TABLE_NAME = '{_tableEvents}')
            BEGIN
                CREATE TABLE {_tableSchema}.{_tableEvents} (
	                Id NVARCHAR(255) NOT NULL,
                    ActorName NVARCHAR(255) NOT NULL,
	                EventIndex BIGINT NOT NULL,
                    EventData NVARCHAR(MAX) NOT NULL,
                    Created DATETIME NOT NULL CONSTRAINT [DF_{_tableEvents}_Created] DEFAULT (getdate()),
                    CONSTRAINT PK_{_tableEvents} PRIMARY KEY CLUSTERED (
                    Id ASC
                ));
                CREATE INDEX IX_{_tableEvents}_ActorNameAndEventIndex ON {_tableSchema}.{_tableEvents}(ActorName ASC, EventIndex ASC);
            END
            ";

            ExecuteNonQuery(sql);
        }

        private static SqlParameter CreateParameter(string name, SqlDbType type, object value)
            => new SqlParameter(name, type)
            {
                SqlValue = value
            };

        private async Task ExecuteNonQueryAsync(string sql, params SqlParameter[] parameters)
        {
            using var connection = new SqlConnection(_connectionString);

            using var command = new SqlCommand(sql, connection);

            await connection.OpenAsync();

            using var tx = connection.BeginTransaction();

            command.Transaction = tx;

            if (parameters.Length > 0)
            {
                command.Parameters.AddRange(parameters);
            }

            await command.ExecuteNonQueryAsync();

            tx.Commit();
        }

        private void ExecuteNonQuery(string sql)
        {
            using var connection = new SqlConnection(_connectionString);

            using var command = new SqlCommand(sql, connection);

            command.ExecuteNonQuery();
        }
    }
}