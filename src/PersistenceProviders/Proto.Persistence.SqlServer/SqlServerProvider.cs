namespace Proto.Persistence.SqlServer
{
    public class SqlServerProvider : IProvider
    {
        private readonly string _connectionString;
        private readonly bool _autoCreateTables;
        private readonly string _useTablesWithPrefix;
        private readonly string _useTablesWithSchema;

        public SqlServerProvider(string connectionString, bool autoCreateTables = false, string useTablesWithPrefix = "", string useTablesWithSchema = "dbo")
        {
            _connectionString = connectionString;
            _autoCreateTables = autoCreateTables;
            _useTablesWithPrefix = useTablesWithPrefix;
            _useTablesWithSchema = useTablesWithSchema;
        }

        public IEventState GetEventState()
        {
            return new SqlServerProviderState(_connectionString, _autoCreateTables, _useTablesWithPrefix, _useTablesWithSchema);
        }

        public ISnapshotState GetSnapshotState()
        {
            return new SqlServerProviderState(_connectionString, _autoCreateTables, _useTablesWithPrefix, _useTablesWithSchema);
        }
    }
}
