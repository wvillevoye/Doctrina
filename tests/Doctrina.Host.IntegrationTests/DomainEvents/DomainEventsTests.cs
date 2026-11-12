using Doctrina.Core.Events;
using Doctrina.Host.IntegrationTests.DataPersistence;
using Doctrina.Infrastructure.Configuration;
using Doctrina.Infrastructure.Events;
using Doctrina.Infrastructure.Persistence;
using Doctrina.TestKit.Modules;
using Doctrina.TestKit.Modules.Data;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

// Tests volgen spec openspec/changes/add-modular-foundation/specs/domain-events/spec.md:
// klasse = requirement, methode = scenario.
namespace Doctrina.Host.IntegrationTests.DomainEvents;

internal static class EventHosts
{
    /// <summary>Module A publiceert; module B handelt <see cref="SampleCreated"/> af.</summary>
    public static TestModule ModuleB(params Action<IServiceCollection>[] handlers) =>
        new("B", "A") { Services = services => Array.ForEach(handlers, h => h(services)) };

    public static void Recording(IServiceCollection services) =>
        services.AddEventHandler<SampleCreated, RecordingSampleCreatedHandler>();

    public static void Failing(IServiceCollection services) =>
        services.AddEventHandler<SampleCreated, FailingSampleCreatedHandler>();

    /// <summary>Host met een <see cref="EventRecorder"/> en opgevangen logs.</summary>
    public static WebApplicationFactory<Program> WithRecorder(
        this WebApplicationFactory<Program> host,
        EventRecorder recorder,
        CapturingLoggerProvider? logs = null) =>
        host.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
        {
            services.AddSingleton(recorder);
            if (logs is not null)
            {
                services.AddSingleton<ILoggerProvider>(logs);
            }
        }));

    /// <summary>Publiceert namens module A via de eventbus in een eigen scope.</summary>
    public static async Task PublishAsync<TEvent>(this WebApplicationFactory<Program> host, TEvent domainEvent, CancellationToken cancellationToken)
        where TEvent : IDomainEvent
    {
        await using var scope = host.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<IEventBus>().PublishAsync(domainEvent, cancellationToken);
    }
}

public sealed class EventsBereikenAlleHandlersVanIngeschakeldeModules(DoctrinaHostFactory factory) : IClassFixture<DoctrinaHostFactory>
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task EventBereiktHandlersInAndereModules()
    {
        var recorder = new EventRecorder();
        using var host = factory.WithModules([TestModules.A(), EventHosts.ModuleB(EventHosts.Recording)], "A", "B").WithRecorder(recorder);

        await host.PublishAsync(new SampleCreated("i6123456"), Ct);

        var received = Assert.Single(recorder.Received);
        Assert.Equal(nameof(RecordingSampleCreatedHandler), received.Handler);
        Assert.Equal(new SampleCreated("i6123456"), received.Event);
    }

    [Fact]
    public async Task HandlerVanEenUitgeschakeldeModuleWordtNietAangeroepen()
    {
        var recorder = new EventRecorder();
        using var host = factory.WithModules([TestModules.A(), EventHosts.ModuleB(EventHosts.Recording)], "A").WithRecorder(recorder);

        var exception = await Record.ExceptionAsync(() => host.PublishAsync(new SampleCreated("i6123456"), Ct));

        Assert.Null(exception);
        Assert.Empty(recorder.Received);
    }

    [Fact]
    public async Task EventZonderHandlers()
    {
        var recorder = new EventRecorder();
        using var host = factory.WithModules([TestModules.A(), EventHosts.ModuleB(EventHosts.Recording)], "A", "B").WithRecorder(recorder);

        var exception = await Record.ExceptionAsync(() => host.PublishAsync(new NobodyListens("x"), Ct));

        Assert.Null(exception);
        Assert.Empty(recorder.Received);
    }
}

public sealed class EenFalendeHandlerRaaktDePublicerendeModuleEnAndereHandlersNiet(DoctrinaHostFactory factory) : IClassFixture<DoctrinaHostFactory>
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task EenHandlerFaaltDeAndereDraaienGewoon()
    {
        var recorder = new EventRecorder();
        using var logs = new CapturingLoggerProvider();
        using var host = factory
            .WithModules([TestModules.A(), EventHosts.ModuleB(EventHosts.Failing, EventHosts.Recording)], "A", "B")
            .WithRecorder(recorder, logs);

        var exception = await Record.ExceptionAsync(() => host.PublishAsync(new SampleCreated("i6123456"), Ct));

        Assert.Null(exception);
        Assert.Single(recorder.Received);
        var error = Assert.Single(logs.Logs, l => l.Level == LogLevel.Error && l.Category == typeof(InProcessEventBus).FullName);
        Assert.Contains(nameof(FailingSampleCreatedHandler), error.Message, StringComparison.Ordinal);
        Assert.Contains(nameof(SampleCreated), error.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("i6123456", error.Message, StringComparison.Ordinal); // geen inhoud van het event in de log
        Assert.IsType<InvalidOperationException>(error.Exception);
    }
}

public sealed class EventsWordenPasNaHetOpslaanGepubliceerd(DoctrinaHostFactory factory) : DatabaseTest, IClassFixture<DoctrinaHostFactory>
{
    [Fact]
    public async Task GeenEventAlsHetOpslaanMislukt()
    {
        var recorder = new EventRecorder();
        using var host = factory
            .WithModules([new SampleModule(), EventHosts.ModuleB(EventHosts.Recording), TestModules.A()], SampleModule.ModuleName, "A", "B")
            .WithRecorder(recorder)
            .InEnvironment(DoctrinaConfigurationExtensions.TestEnvironment, Database, (ModuleMigrationService.MigrateOnStartupKey, "true"));
        host.CreateClient().Dispose();

        // Eerst een geslaagde opslag: het event komt pas ná SaveChanges.
        await using (var scope = host.Services.CreateAsyncScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<SampleDbContext>();
            context.Items.Add(SampleItem.Create("i6123456"));
            Assert.Empty(recorder.Received);
            await context.SaveChangesAsync(Ct);
        }

        Assert.Single(recorder.Received);

        // Dan een opslag die mislukt (dubbele ExternalId binnen dezelfde bron): geen nieuw event.
        await using (var scope = host.Services.CreateAsyncScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<SampleDbContext>();
            var duplicate = SampleItem.Create("i6123456");
            context.Items.Add(duplicate);

            var failure = await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync(Ct));

            Assert.True(failure.IsUniqueConstraintViolation());
            Assert.Single(duplicate.DomainEvents); // blijft staan voor een volgende poging
        }

        Assert.Single(recorder.Received);
    }
}
