using System.Collections.Concurrent;
using System.Net;
using Doctrina.TestKit.Authentication;
using Hangfire;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Doctrina.Host.IntegrationTests.Academic;

/// <summary>A job for the tests: records that it ran.</summary>
public static class MarkerJob
{
    private static readonly ConcurrentDictionary<string, bool> Ran = new(StringComparer.Ordinal);

    public static void Mark(string id) => Ran[id] = true;

    public static async Task WaitForAsync(string id, CancellationToken ct)
    {
        var deadline = DateTime.UtcNow.AddSeconds(30);
        while (!Ran.ContainsKey(id))
        {
            Assert.True(DateTime.UtcNow < deadline, $"Job {id} did not run within 30 s.");
            await Task.Delay(200, ct);
        }
    }
}

// Spec background-jobs, requirement "Achtergrondtaken draaien met Hangfire".
public sealed class AchtergrondtakenDraaienMetHangfire(DoctrinaHostFactory factory) : IClassFixture<DoctrinaHostFactory>
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task IngeplandeTaakWordtUitgevoerd()
    {
        await using var academic = await AcademicHost.CreateAsync(factory, Ct);
        // Test ids are not entity keys (house rule 5 is about keys).
        var id = Guid.NewGuid().ToString("N");

        academic.Host.Services.GetRequiredService<IBackgroundJobClient>().Enqueue(() => MarkerJob.Mark(id));

        await MarkerJob.WaitForAsync(id, Ct);
    }

    [Fact]
    public async Task SchemaVanHangfire()
    {
        await using var academic = await AcademicHost.CreateAsync(factory, Ct);

        var tables = await academic.QueryAsync(db => db.Database
            .SqlQueryRaw<string>("SELECT TABLE_NAME AS [Value] FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_SCHEMA = 'hangfire'")
            .ToListAsync(Ct));

        Assert.Contains("Job", tables);
        Assert.Contains("JobQueue", tables);
    }
}

// Spec background-jobs, requirement "Het dashboard is alleen voor beheerders".
public sealed class HetDashboardIsAlleenVoorBeheerders(DoctrinaHostFactory factory) : IClassFixture<DoctrinaHostFactory>
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task BeheerderOpentHetDashboard()
    {
        await using var academic = await AcademicHost.CreateAsync(factory, Ct);

        var (status, html) = await academic.GetAsync(factory, StubUser.Beheerder, "/hangfire", Ct);

        Assert.Equal(HttpStatusCode.OK, status);
        Assert.Contains("Hangfire", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task StudentOpentHetDashboard()
    {
        await using var academic = await AcademicHost.CreateAsync(factory, Ct);

        var (status, _) = await academic.GetAsync(factory, StubUser.Student, "/hangfire", Ct);

        Assert.Equal(HttpStatusCode.Forbidden, status);
    }

    [Fact]
    public async Task AnoniemOpentHetDashboard()
    {
        await using var academic = await AcademicHost.CreateAsync(factory, Ct);
        using var browser = academic.Host.CreateBrowser();

        using var response = await browser.GetAsync(Json.Url("/hangfire"), Ct);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
