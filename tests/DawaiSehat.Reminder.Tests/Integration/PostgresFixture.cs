using DawaiSehat.Reminder.Data;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Testcontainers.PostgreSql;

namespace DawaiSehat.Reminder.Tests.Integration;

/// <summary>
/// Starts ONE throwaway PostgreSQL container (Docker must be running) for all integration tests of the
/// "postgres" collection, and gives every test its own fresh, migrated database inside it.
/// </summary>
public sealed class PostgresFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("postgres:17-alpine").Build();

    public Task InitializeAsync() => _container.StartAsync();

    public Task DisposeAsync() => _container.DisposeAsync().AsTask();

    /// <summary>Creates an empty database and returns its connection string (migrations NOT applied).</summary>
    public async Task<string> CreateEmptyDatabaseAsync()
    {
        var name = "t_" + Guid.NewGuid().ToString("N");
        await using var connection = new NpgsqlConnection(_container.GetConnectionString());
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand($"CREATE DATABASE {name}", connection);
        await command.ExecuteNonQueryAsync();

        return new NpgsqlConnectionStringBuilder(_container.GetConnectionString()) { Database = name }.ConnectionString;
    }

    /// <summary>Creates a database with all migrations applied, the same way a real deployment does.</summary>
    public async Task<string> CreateMigratedDatabaseAsync()
    {
        var connectionString = await CreateEmptyDatabaseAsync();
        await using var db = CreateContext(connectionString);
        await db.Database.MigrateAsync();
        return connectionString;
    }

    public static ReminderDbContext CreateContext(string connectionString) =>
        new(new DbContextOptionsBuilder<ReminderDbContext>().UseNpgsql(connectionString).Options);
}

[CollectionDefinition("postgres")]
public sealed class PostgresCollection : ICollectionFixture<PostgresFixture>;
