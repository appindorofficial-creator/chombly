using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using WebAppPet.Data;

namespace WebAppPet.Tests.Support;

/// <summary>
/// In-memory SQLite database that lives as long as the instance. Use <see cref="CreateContext"/>
/// to assert through a fresh context (no tracked entities from the code under test).
/// </summary>
public sealed class TestDatabase : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<AppDbContext> _options;

    public TestDatabase()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        _options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(_connection)
            .Options;

        using var db = CreateContext();
        db.Database.EnsureCreated();
    }

    public AppDbContext CreateContext() => new(_options);

    public void Dispose() => _connection.Dispose();
}
