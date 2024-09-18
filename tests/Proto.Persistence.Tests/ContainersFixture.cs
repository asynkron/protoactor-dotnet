using System.Threading.Tasks;
using Testcontainers.MongoDb;
using Testcontainers.MsSql;
using Testcontainers.PostgreSql;
using Xunit;

namespace Proto.Persistence.Tests;

public class ContainersFixture : IAsyncLifetime
{
    private const int InitialState = 1;

    private readonly PostgreSqlContainer _postgreSqlContainer = new PostgreSqlBuilder()
        .WithDatabase("IntegrationTests")
        .WithUsername("postgres")
        .WithPassword("root")
        .WithCommand(new[] { "-c", "log_statement=all" })
        .Build();

    readonly MsSqlContainer _msSqlContainer = new MsSqlBuilder()
        .Build();

    readonly MongoDbContainer _mongoDbContainer = new MongoDbBuilder()
        .Build();

    public PostgreSqlContainer Postgres => _postgreSqlContainer;

    public MsSqlContainer MsSql => _msSqlContainer;

    public MongoDbContainer MongoDb => _mongoDbContainer;

    public Task InitializeAsync() =>
        Task.WhenAll(
            _postgreSqlContainer.StartAsync(),
            _msSqlContainer.StartAsync(),
            _mongoDbContainer.StartAsync()
        );

    public Task DisposeAsync() =>
        Task.WhenAll(
            _postgreSqlContainer.StopAsync(),
            _msSqlContainer.StopAsync(),
            _mongoDbContainer.StopAsync()
        );
}