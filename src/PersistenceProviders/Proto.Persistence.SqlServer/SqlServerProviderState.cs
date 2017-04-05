using System;
using System.Threading.Tasks;
using System.Data.SqlClient;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace Proto.Persistence.SqlServer
{
    public class SqlServerProviderState : IProviderState
    {
        private readonly string _connectionString;
        private readonly string _tableSnapshots;
        private readonly string _tableEvents;
        private readonly string _tableSchema;

        public SqlServerProviderState(string connectionString, bool autoCreateTables, string useTablesWithPrefix, string useTablesWithSchema)
        {
            _connectionString = connectionString;
            _tableSchema = useTablesWithSchema;
            _tableSnapshots = string.IsNullOrEmpty(useTablesWithPrefix) ? "Snapshots" : $"{useTablesWithPrefix}_Snapshots";
            _tableEvents = string.IsNullOrEmpty(useTablesWithPrefix) ? "Events" : $"{useTablesWithPrefix}_Events";

            if (autoCreateTables)
            {
                AutoCreateSnapshotTableInDatabaseAsync().Wait();
                AutoCreateEventTableInDatabaseAsync().Wait();
            }
        }

        private async Task ExecuteNonQueryAsync(string sql, List<SqlParameter> parameters = null)
        {
            try
            {
                using (var connection = new SqlConnection(_connectionString))
                using (var command = new SqlCommand(sql, connection))
                {
                    await connection.OpenAsync();

                    using (var tx = connection.BeginTransaction())
                    {
                        command.Transaction = tx;

                        if (parameters?.Count > 0)
                        {
                            foreach (var p in parameters)
                            {
                                command.Parameters.Add(p);
                            }
                        }

                        await command.ExecuteNonQueryAsync();

                        tx.Commit();
                    }

                    connection.Close();
                }
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }

        private async Task AutoCreateSnapshotTableInDatabaseAsync()
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

            await ExecuteNonQueryAsync(sql);
        }

        private async Task AutoCreateEventTableInDatabaseAsync()
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

            await ExecuteNonQueryAsync(sql);
        }
        
        public async Task DeleteEventsAsync(string actorName, long inclusiveToIndex)
        {
            var sql = $@"DELETE FROM {_tableSchema}.{_tableEvents} WHERE ActorName = @ActorName AND EventIndex <= @EventIndex";

            var parameters = new List<SqlParameter>
            {
                new SqlParameter()
                {
                    ParameterName = "ActorName",
                    SqlDbType = System.Data.SqlDbType.NVarChar,
                    SqlValue = actorName
                },
                new SqlParameter()
                {
                    ParameterName = "EventIndex",
                    SqlDbType = System.Data.SqlDbType.BigInt,
                    SqlValue = inclusiveToIndex
                }
            };

            await ExecuteNonQueryAsync(sql, parameters);
        }

        public async Task DeleteSnapshotsAsync(string actorName, long inclusiveToIndex)
        {
            var sql = $@"DELETE FROM {_tableSchema}.{_tableSnapshots} WHERE ActorName = @ActorName AND SnapshotIndex <= @SnapshotIndex";

            var parameters = new List<SqlParameter>
            {
                new SqlParameter()
                {
                    ParameterName = "ActorName",
                    SqlDbType = System.Data.SqlDbType.NVarChar,
                    SqlValue = actorName
                },
                new SqlParameter()
                {
                    ParameterName = "SnapshotIndex",
                    SqlDbType = System.Data.SqlDbType.BigInt,
                    SqlValue = inclusiveToIndex
                }
            };

            await ExecuteNonQueryAsync(sql, parameters);
        }

        public async Task GetEventsAsync(string actorName, long indexStart, Action<object> callback)
        {
            var sql = $@"SELECT EventData FROM {_tableSchema}.{_tableEvents} WHERE ActorName = @ActorName AND EventIndex >= @EventIndex ORDER BY EventIndex ASC";

            try
            {
                using (var connection = new SqlConnection(_connectionString))
                using (var command = new SqlCommand(sql, connection))
                {
                    await connection.OpenAsync();

                    command.Parameters.Add(new SqlParameter()
                    {
                        ParameterName = "ActorName",
                        SqlDbType = System.Data.SqlDbType.NVarChar,
                        SqlValue = actorName
                    });

                    command.Parameters.Add(new SqlParameter()
                    {
                        ParameterName = "EventIndex",
                        SqlDbType = System.Data.SqlDbType.BigInt,
                        SqlValue = indexStart
                    });

                    var eventReader = await command.ExecuteReaderAsync();

                    while(await eventReader.ReadAsync())
                    {
                        callback(JsonConvert.DeserializeObject<object>(eventReader["EventData"].ToString(), new JsonSerializerSettings { TypeNameHandling = TypeNameHandling.Auto }));
                    }

                    connection.Close();
                }
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }

        public async Task<(object Snapshot, long Index)> GetSnapshotAsync(string actorName)
        {
            long snapshotIndex = 0;
            object snapshotData = null;

            var sql = $@"SELECT TOP 1 SnapshotIndex, SnapshotData FROM {_tableSchema}.{_tableSnapshots} WHERE ActorName = @ActorName ORDER BY SnapshotIndex DESC";

            try
            {
                using (var connection = new SqlConnection(_connectionString))
                using (var command = new SqlCommand(sql, connection))
                {
                    await connection.OpenAsync();

                    command.Parameters.Add(new SqlParameter()
                    {
                        ParameterName = "ActorName",
                        SqlDbType = System.Data.SqlDbType.NVarChar,
                        SqlValue = actorName
                    });

                    var snapshotReader = await command.ExecuteReaderAsync();

                    while (await snapshotReader.ReadAsync())
                    {
                        snapshotIndex = Convert.ToInt64(snapshotReader["SnapshotIndex"]);
                        snapshotData = JsonConvert.DeserializeObject<object>(snapshotReader["SnapshotData"].ToString(), new JsonSerializerSettings { TypeNameHandling = TypeNameHandling.Auto });
                    }

                    connection.Close();
                }
            }
            catch (Exception ex)
            {
                throw ex;
            }

            return (snapshotData, snapshotIndex);
        }

        public async Task PersistEventAsync(string actorName, long index, object @event)
        {
            var item = new Event(actorName, index, @event);

            var sql = $@"INSERT INTO {_tableSchema}.{_tableEvents} (Id, ActorName, EventIndex, EventData) VALUES (@Id, @ActorName, @EventIndex, @EventData)";

            var parameters = new List<SqlParameter>
            {
                new SqlParameter()
                {
                    ParameterName = "Id",
                    SqlDbType = System.Data.SqlDbType.NVarChar,
                    SqlValue = item.Id
                },
                new SqlParameter()
                {
                    ParameterName = "ActorName",
                    SqlDbType = System.Data.SqlDbType.NVarChar,
                    SqlValue = item.ActorName
                },
                new SqlParameter()
                {
                    ParameterName = "EventIndex",
                    SqlDbType = System.Data.SqlDbType.BigInt,
                    SqlValue = item.EventIndex
                },
                new SqlParameter()
                {
                    ParameterName = "EventData",
                    SqlDbType = System.Data.SqlDbType.NVarChar,
                    SqlValue = JsonConvert.SerializeObject(item.EventData, new JsonSerializerSettings { TypeNameHandling = TypeNameHandling.All })
                }
            };

            await ExecuteNonQueryAsync(sql, parameters);
        }

        public async Task PersistSnapshotAsync(string actorName, long index, object snapshot)
        {
            var item = new Snapshot(actorName, index, snapshot);

            var sql = $@"INSERT INTO {_tableSchema}.{_tableSnapshots} (Id, ActorName, SnapshotIndex, SnapshotData) VALUES (@Id, @ActorName, @SnapshotIndex, @SnapshotData)";

            var parameters = new List<SqlParameter>
            {
                new SqlParameter()
                {
                    ParameterName = "Id",
                    SqlDbType = System.Data.SqlDbType.NVarChar,
                    SqlValue = item.Id
                },
                new SqlParameter()
                {
                    ParameterName = "ActorName",
                    SqlDbType = System.Data.SqlDbType.NVarChar,
                    SqlValue = item.ActorName
                },
                new SqlParameter()
                {
                    ParameterName = "SnapshotIndex",
                    SqlDbType = System.Data.SqlDbType.BigInt,
                    SqlValue = item.SnapshotIndex
                },
                new SqlParameter()
                {
                    ParameterName = "SnapshotData",
                    SqlDbType = System.Data.SqlDbType.NVarChar,
                    SqlValue = JsonConvert.SerializeObject(item.SnapshotData, new JsonSerializerSettings { TypeNameHandling = TypeNameHandling.All })
                }
            };

            await ExecuteNonQueryAsync(sql, parameters);
        }
    }
}
