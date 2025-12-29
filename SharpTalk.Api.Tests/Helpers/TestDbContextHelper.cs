using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using SharpTalk.Api.Data;
using SharpTalk.Api.Entities;

namespace SharpTalk.Api.Tests.Helpers;

/// <summary>
/// Helper class for creating in-memory SQLite test databases.
/// </summary>
public class TestDbContextHelper : IDisposable
{
    private readonly SqliteConnection _connection;

    public ApplicationDbContext Context { get; }

    public TestDbContextHelper()
    {
        // Create and open a connection. This creates the in-memory database.
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(_connection)
            .Options;

        Context = new ApplicationDbContext(options);
        Context.Database.EnsureCreated();
    }

    /// <summary>
    /// Seeds the database with common test data.
    /// </summary>
    public async Task SeedTestDataAsync()
    {
        // Create test users
        var user1 = new User
        {
            Id = 1,
            Username = "testuser1",
            Email = "test1@example.com",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("password123"),
            CreatedAt = DateTime.UtcNow,
            Status = "Online"
        };

        var user2 = new User
        {
            Id = 2,
            Username = "testuser2",
            Email = "test2@example.com",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("password123"),
            CreatedAt = DateTime.UtcNow,
            Status = "Online"
        };

        Context.Users.AddRange(user1, user2);

        // Create a test workspace
        var workspace = new Workspace
        {
            Id = 1,
            Name = "Test Workspace",
            OwnerId = 1,
            CreatedAt = DateTime.UtcNow
        };

        Context.Workspaces.Add(workspace);

        // Add user1 as workspace member (owner)
        var workspaceMember = new WorkspaceMember
        {
            Id = 1,
            WorkspaceId = 1,
            UserId = 1,
            Role = "Owner",
            JoinedAt = DateTime.UtcNow,
            OrderIndex = 0
        };

        Context.WorkspaceMembers.Add(workspaceMember);

        // Create a default channel
        var channel = new Channel
        {
            Id = 1,
            WorkspaceId = 1,
            Name = "general",
            IsPrivate = false
        };

        Context.Channels.Add(channel);

        await Context.SaveChangesAsync();
    }

    /// <summary>
    /// Creates a fresh in-memory database context for each test.
    /// </summary>
    public static TestDbContextHelper Create()
    {
        return new TestDbContextHelper();
    }

    public void Dispose()
    {
        Context.Dispose();
        _connection.Dispose();
    }
}
