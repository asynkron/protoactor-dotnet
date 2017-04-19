using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Proto.Persistence.Sqlite
{
    public class SqlitePersistenceContext : DbContext
    {
        private readonly string _datasource;

        public SqlitePersistenceContext(string datasource)
        {
            _datasource = datasource;
        }

        public DbSet<Snapshot> Snapshots { get; set; }
        public DbSet<Event> Events { get; set; }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            var connectionStringBuilder = new SqliteConnectionStringBuilder { DataSource = _datasource };
            var connectionString = connectionStringBuilder.ToString();
            var connection = new SqliteConnection(connectionString);

            optionsBuilder.UseSqlite(connection);
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Snapshot>()
                .HasKey(c => c.Id);

            modelBuilder.Entity<Event>()
                .HasKey(c => c.Id);
        }
    }
}
