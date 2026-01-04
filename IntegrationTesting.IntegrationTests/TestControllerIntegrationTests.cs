using System;
using System.Net;
using System.Threading.Tasks;
using Testcontainers.PostgreSql;
using System.Collections.Generic;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Npgsql;
using Xunit;

namespace IntegrationTesting.IntegrationTests
{
    public class PostgresTestcontainerFixture : IAsyncLifetime
    {
        public Testcontainers.PostgreSql.PostgreSqlContainer? Container { get; private set; }
        public string Database { get; } = "testdb";
        public string? ConnectionString { get; private set; }

        public async Task InitializeAsync()
        {
            // Disable Ryuk (resource reaper) when Docker Hub auth is restricted
            // Tests will stop and dispose containers explicitly in DisposeAsync.
            Environment.SetEnvironmentVariable("TESTCONTAINERS_RYUK_DISABLED", "true");
            var builder = new Testcontainers.PostgreSql.PostgreSqlBuilder("postgres:15-alpine")
                .WithDatabase(Database)
                .WithUsername("postgres")
                .WithPassword("postgres");

            try
            {
                Container = builder.Build();
                await Container.StartAsync();
                ConnectionString = Container.GetConnectionString();
            }
            catch (Exception ex)
            {
                // Fallback to environment-provided connection string when Docker Hub pull is restricted
                var env = Environment.GetEnvironmentVariable("TEST_DATABASE_CONNECTION");
                if (!string.IsNullOrEmpty(env))
                {
                    ConnectionString = env;
                }
                else
                {
                    // Default fallback to a locally-available Postgres instance
                    ConnectionString = "Host=localhost;Port=5432;Database=postgres;Username=postgres;Password=postgres";
                }
            }
        }

        public async Task DisposeAsync()
        {
            if (Container != null)
            {
                await Container.StopAsync();
                await Container.DisposeAsync();
            }
        }
    }

    [CollectionDefinition("Postgres collection")]
    public class PostgresCollection : ICollectionFixture<PostgresTestcontainerFixture>
    {
    }

    [Collection("Postgres collection")]
    public class TestControllerIntegrationTests
    {
        private readonly PostgresTestcontainerFixture _fixture;

        public TestControllerIntegrationTests(PostgresTestcontainerFixture fixture)
        {
            _fixture = fixture;
        }

        [Fact]
        public async Task GetNameById_ReturnsInsertedName()
        {
            var connString = _fixture.ConnectionString ?? _fixture.Container?.GetConnectionString();
            if (string.IsNullOrEmpty(connString)) throw new InvalidOperationException("No connection string available for integration test.");

            const int id = 9999;
            const string name = "IntegrationTestName";

            await using (var conn = new NpgsqlConnection(connString))
            {
                await conn.OpenAsync();
                await using var createCmd = new NpgsqlCommand("CREATE TABLE IF NOT EXISTS \"Test\" (\"Id\" integer PRIMARY KEY, \"Name\" text);", conn);
                await createCmd.ExecuteNonQueryAsync();

                await using var upsertCmd = new NpgsqlCommand("INSERT INTO \"Test\" (\"Id\", \"Name\") VALUES (@id, @name) ON CONFLICT (\"Id\") DO UPDATE SET \"Name\" = EXCLUDED.\"Name\";", conn);
                upsertCmd.Parameters.AddWithValue("id", id);
                upsertCmd.Parameters.AddWithValue("name", name);
                await upsertCmd.ExecuteNonQueryAsync();
            }

            var factory = new WebApplicationFactory<IntegrationTesting.API.Program>()
                .WithWebHostBuilder(builder =>
                {
                    builder.ConfigureAppConfiguration((context, conf) =>
                    {
                        var dict = new Dictionary<string, string>
                        {
                            { "ConnectionStrings:DefaultConnection", connString }
                        };
                        conf.AddInMemoryCollection(dict);
                    });
                });

            var client = factory.CreateClient();
            var resp = await client.GetAsync($"/test/{id}");
            Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
            var content = await resp.Content.ReadAsStringAsync();
            Assert.Equal(name, content.Trim('"'));
        }
    }
}
