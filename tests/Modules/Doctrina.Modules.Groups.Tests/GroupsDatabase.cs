global using Doctrina.TestKit.Fakes;
using Doctrina.Core.Events;
using Doctrina.Infrastructure.Events;
using Doctrina.Infrastructure.Persistence;
using Doctrina.Modules.Groups.Contracts;
using Doctrina.Modules.Groups.Infrastructure;
using Doctrina.TestKit.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace Doctrina.Modules.Groups.Tests;

/// <summary>
/// An empty LocalDB database with the Groups migrations applied, per test, with an event bus that records
/// <see cref="GroupMembershipChanged"/>. The base connection string comes from <c>src/Doctrina.Host/appsettings.Test.json</c>.
/// </summary>
internal sealed class GroupsDatabase : IAsyncDisposable
{
    private static readonly Lazy<string> BaseConnectionString = new(() =>
        new ConfigurationBuilder()
            .AddJsonFile(Path.Combine(RepoRoot(), "src", "Doctrina.Host", "appsettings.Test.json"), optional: false)
            .Build()
            .GetConnectionString(ModuleDatabase.ConnectionStringName)
        ?? throw new InvalidOperationException("ConnectionStrings:Doctrina is missing in appsettings.Test.json."));

    private readonly LocalDbTestDatabase _database;
    private readonly InProcessEventBus _events;

    private GroupsDatabase(LocalDbTestDatabase database)
    {
        _database = database;
        var services = new ServiceCollection()
            .AddLogging()
            .AddSingleton<IEventHandler<GroupMembershipChanged>>(Events)
            .BuildServiceProvider();
        _events = new InProcessEventBus(services, NullLogger<InProcessEventBus>.Instance);
    }

    public EventRecorder Events { get; } = new();

    public static async Task<GroupsDatabase> CreateAsync(CancellationToken cancellationToken)
    {
        var database = new GroupsDatabase(await LocalDbTestDatabase.CreateAsync(BaseConnectionString.Value, cancellationToken));
        await using var context = database.CreateContext();
        await context.Database.MigrateAsync(cancellationToken);
        return database;
    }

    public LocalDbTestDatabase Database => _database;

    /// <summary>A fresh context (own change tracker), like a new request.</summary>
    public GroupsDbContext CreateContext() =>
        new(ModuleDatabase.CreateOptions<GroupsDbContext>(_database.ConnectionString, GroupsModule.ModuleName, events: _events));

    public ValueTask DisposeAsync() => _database.DisposeAsync();

    public static string RepoRoot()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            if (File.Exists(Path.Combine(dir.FullName, "Doctrina.slnx")))
            {
                return dir.FullName;
            }
        }

        throw new InvalidOperationException("Doctrina.slnx not found above " + AppContext.BaseDirectory);
    }
}

/// <summary>Records published membership events.</summary>
internal sealed class EventRecorder : IEventHandler<GroupMembershipChanged>
{
    public List<GroupMembershipChanged> Published { get; } = [];

    public Task HandleAsync(GroupMembershipChanged domainEvent, CancellationToken cancellationToken)
    {
        Published.Add(domainEvent);
        return Task.CompletedTask;
    }
}

// The in-memory IAcademicDirectory lives in Doctrina.TestKit (Fakes/FakeDirectory.cs) since add-content.
