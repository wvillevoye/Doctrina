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

    /// <param name="factory">The shared host factory.</param>
    /// <param name="cancellationToken">Cancels creating the database.</param>
    /// <param name="enable">Further modules to switch on next to Academic (known product modules by name, or test modules).</param>
    public static Task<AcademicHost> CreateAsync(DoctrinaHostFactory factory, CancellationToken cancellationToken, params object[] enable) =>
        CreateCoreAsync(factory, [], enable, cancellationToken);

    /// <summary>
    /// As above, with extra settings (e.g. <c>Api:Sources:…</c>). API callers: role <c>test.read</c> has
    /// <c>Academic.Read</c> (and <c>Groups.Read</c>), role <c>test.write</c> has <c>Academic.Write</c>.
    /// </summary>
    public static Task<AcademicHost> CreateAsync(DoctrinaHostFactory factory, (string Key, string? Value)[] settings, CancellationToken cancellationToken) =>
        CreateCoreAsync(factory, settings, [], cancellationToken);

    private static async Task<AcademicHost> CreateCoreAsync(
        DoctrinaHostFactory factory, (string Key, string? Value)[] settings, object[] enable, CancellationToken cancellationToken)
    {
        var database = await TestDatabases.CreateAsync(cancellationToken);
        var modules = enable.OfType<Doctrina.Core.Modules.IModule>().ToList();
        string[] names = [AcademicModule.ModuleName, .. enable.Select(e => e is Doctrina.Core.Modules.IModule m ? m.Name : (string)e)];
        var host = factory.WithModules(modules, names).InEnvironment(
            DoctrinaConfigurationExtensions.TestEnvironment,
            database,
            [
                (ModuleMigrationService.MigrateOnStartupKey, "true"),
                (AccessHosts.Mapping("student", 0), AcademicPermissions.MyCourses),
                (AccessHosts.Mapping("employee", 0), AcademicPermissions.MyCourses),
                (AccessHosts.Mapping("test.read", 0), AcademicPermissions.Read),
                (AccessHosts.Mapping("test.read", 1), "Groups.Read"),
                (AccessHosts.Mapping("test.write", 0), AcademicPermissions.Write),
                .. settings,
            ]);
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

    /// <summary>An API client for system <paramref name="client"/> (claim <c>azp</c>) with <paramref name="roles"/>.</summary>
    public HttpClient Api(DoctrinaHostFactory root, string client = "doctrina-sis-sync", params string[] roles)
    {
        var http = Host.CreateBrowser();
        http.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue(
            "Bearer", root.Idp.CreateAccessToken(DoctrinaHostFactory.ApiAudience, client, roles.Length == 0 ? ["test.read", "test.write"] : roles));
        return http;
    }

    public static Dictionary<string, string?> Record(params (string Name, string? Value)[] fields) =>
        fields.ToDictionary(f => f.Name, f => f.Value, StringComparer.OrdinalIgnoreCase);

    public async ValueTask DisposeAsync()
    {
        await Host.DisposeAsync();
        await _database.DisposeAsync();
    }
}
