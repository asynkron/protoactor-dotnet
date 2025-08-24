using System.Threading.Tasks;
using Testcontainers.MongoDb;
using Testcontainers.MsSql;
using Testcontainers.PostgreSql;
using Xunit;

namespace Proto.Persistence.Tests;

public class ContainersFixture : IAsyncLifetime
{
    public PostgreSqlContainer Postgres { get; } = new PostgreSqlBuilder()
        .WithDatabase("IntegrationTests")
        .WithUsername("postgres")
        .WithPassword("root")
        .WithCommand(new[] { "-c", "log_statement=all" })
        .Build();

    public MsSqlContainer MsSql { get; } = new MsSqlBuilder()
        .Build();

    public MongoDbContainer MongoDb { get; } = new MongoDbBuilder()
        .Build();

    public Task InitializeAsync() =>
        Task.WhenAll(
            Postgres.StartAsync(),
            MsSql.StartAsync(),
            MongoDb.StartAsync()
        );

    public Task DisposeAsync() =>
        Task.WhenAll(
            Postgres.StopAsync(),
            MsSql.StopAsync(),
            MongoDb.StopAsync()
        );
}