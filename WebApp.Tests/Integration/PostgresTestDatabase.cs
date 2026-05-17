using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Npgsql;
using Pgvector.EntityFrameworkCore;
using Pgvector.Npgsql;

namespace WebApp.Tests.Integration;

public sealed class PostgresTestDatabase : IAsyncDisposable
{
    private PostgresTestDatabase(string connectionString)
    {
        ConnectionString = connectionString;
        var dataSourceBuilder = new NpgsqlDataSourceBuilder(connectionString);
        dataSourceBuilder.UseVector();
        DataSource = dataSourceBuilder.Build();
    }

    private string ConnectionString { get; }
    private NpgsqlDataSource DataSource { get; }

    public static async Task<PostgresTestDatabase> CreateAsync()
    {
        var baseConnectionString = Environment.GetEnvironmentVariable("TEST_POSTGRES_CONNECTION")
            ?? "Host=localhost;Port=5432;Database=booknotes_test;Username=postgres;Password=postgres";
        var databaseName = $"booknotes_test_{Guid.NewGuid():N}";
        var connectionString = ReplaceDatabaseName(baseConnectionString, databaseName);
        var database = new PostgresTestDatabase(connectionString);

        await using var db = database.CreateDbContext();
        await db.Database.EnsureDeletedAsync();
        await db.Database.MigrateAsync();
        await database.DataSource.ReloadTypesAsync();

        return database;
    }

    public AppDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(DataSource, npgsql => npgsql.UseVector())
            .ConfigureWarnings(warnings => warnings.Ignore(RelationalEventId.PendingModelChangesWarning))
            .Options;
        return new AppDbContext(options);
    }

    public async ValueTask DisposeAsync()
    {
        await using var db = CreateDbContext();
        await db.Database.EnsureDeletedAsync();
        await DataSource.DisposeAsync();
    }

    private static string ReplaceDatabaseName(string connectionString, string databaseName)
    {
        var parts = connectionString.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var replaced = false;
        for (var i = 0; i < parts.Length; i++)
        {
            if (!parts[i].StartsWith("Database=", StringComparison.OrdinalIgnoreCase))
                continue;

            parts[i] = $"Database={databaseName}";
            replaced = true;
            break;
        }

        return string.Join(';', replaced ? parts : [.. parts, $"Database={databaseName}"]);
    }
}
