using System.Net;
using Doctrina.Host.IntegrationTests.AccessControl;
using Doctrina.Host.IntegrationTests.DataPersistence;
using Doctrina.Infrastructure.Configuration;
using Doctrina.Infrastructure.Persistence;
using Doctrina.Modules.Academic;
using Doctrina.Modules.Academic.Application;
using Doctrina.Modules.Academic.Infrastructure;
using Doctrina.TestKit.Authentication;
using Doctrina.TestKit.Database;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Doctrina.Host.IntegrationTests.Academic;

/// <summary>
/// The host with the Academic module switched on, on its own migrated LocalDB database, with the role mapping of
/// <c>appsettings.Development.json</c> (student and employee get <c>Academic.MyCourses</c>; admin has <c>*</c>).
/// </summary>
internal sealed class AcademicHost : IAsyncDisposable
{
    private readonly LocalDbTestDatabase _database;

    private AcademicHost(LocalDbTestDatabase database, WebApplicationFactory<Program> host)
    {
        _database = database;
        Host = host;
    }

    public WebApplicationFactory<Program> Host { get; }

    public static async Task<AcademicHost> CreateAsync(DoctrinaHostFactory factory, CancellationToken cancellationToken)
    {
        var database = await TestDatabases.CreateAsync(cancellationToken);
        var host = factory.WithModules([], AcademicModule.ModuleName).InEnvironment(
            DoctrinaConfigurationExtensions.TestEnvironment,
            database,
            (ModuleMigrationService.MigrateOnStartupKey, "true"),
            (AccessHosts.Mapping("student", 0), AcademicPermissions.MyCourses),
            (AccessHosts.Mapping("employee", 0), AcademicPermissions.MyCourses));
        host.Services.GetRequiredService<IServiceProvider>(); // start the host, which migrates
        return new AcademicHost(database, host);
    }

    /// <summary>Upserts records from source SIS, like a CSV import.</summary>
    public async Task SeedAsync(string type, params Dictionary<string, string?>[] records)
    {
        using var scope = Host.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AcademicDbContext>();
        foreach (var record in records)
        {
            var result = await AcademicTypes.Find(type)!.UpsertAsync(db, "SIS", record, CancellationToken.None);
            Assert.True(result.IsSuccess, result.Error?.ToString());
        }
    }

    public async Task<T> QueryAsync<T>(Func<AcademicDbContext, Task<T>> query)
    {
        using var scope = Host.Services.CreateScope();
        return await query(scope.ServiceProvider.GetRequiredService<AcademicDbContext>());
    }

    /// <summary>
    /// Signs in <paramref name="user"/>, opens <paramref name="path"/> (following local redirects, e.g. to
    /// <c>/access-denied</c>) and returns status and decoded HTML.
    /// </summary>
    public async Task<(HttpStatusCode Status, string Html)> GetAsync(DoctrinaHostFactory root, StubUser user, string path, CancellationToken ct)
    {
        using var browser = Host.CreateBrowser();
        await browser.SignInAndGetAsync("/profile", root.Idp, user, ct);
        return await GetAsync(browser, path, ct);
    }

    public static async Task<(HttpStatusCode Status, string Html)> GetAsync(HttpClient browser, string path, CancellationToken ct)
    {
        var response = await browser.GetAsync(new Uri(path, UriKind.Relative), ct);
        while (response.StatusCode == HttpStatusCode.Found && response.Headers.Location is { } location
            && (!location.IsAbsoluteUri || location.Host == browser.BaseAddress?.Host))
        {
            response.Dispose();
            response = await browser.GetAsync(location, ct);
        }

        using (response)
        {
            return (response.StatusCode, WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync(ct)));
        }
    }

    public static Dictionary<string, string?> Record(params (string Name, string? Value)[] fields) =>
        fields.ToDictionary(f => f.Name, f => f.Value, StringComparer.OrdinalIgnoreCase);

    public async ValueTask DisposeAsync()
    {
        await Host.DisposeAsync();
        await _database.DisposeAsync();
    }
}
