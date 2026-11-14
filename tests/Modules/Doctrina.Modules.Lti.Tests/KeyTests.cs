using System.Security.Cryptography;
using System.Text.Json;
using Doctrina.Modules.Lti.Application;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;

namespace Doctrina.Modules.Lti.Tests;

// Spec lti-tool-registration of change add-lti-platform, requirement "Doctrina publiceert eigen ondertekeningssleutels";
// task 3.3. Class = requirement, method = scenario.

internal static class KeyTestData
{
    public static KeyService KeyService(this LtiDatabase db, FakeTimeProvider? time = null, string ring = "ring-a", int retireAfterDays = 7) =>
        new(db.CreateContext(), DataProtectionProvider.Create(ring), time ?? new FakeTimeProvider(), Options.Create(new LtiOptions { Keys = { RetireAfterDays = retireAfterDays } }));

    public static string[] Kids(this JsonElement jwks) =>
        [.. jwks.GetProperty("keys").EnumerateArray().Select(k => k.GetProperty("kid").GetString()!)];
}

public sealed class DoctrinaPubliceertEigenOndertekeningssleutels
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task EersteAanvraagMaaktEenSleutelAan()
    {
        await using var db = await LtiDatabase.CreateAsync(Ct);

        var jwks = await db.KeyService().JwksAsync(Ct);

        var key = Assert.Single(jwks.GetProperty("keys").EnumerateArray());
        Assert.Equal("RSA", key.GetProperty("kty").GetString());
        Assert.Equal("RS256", key.GetProperty("alg").GetString());
        Assert.Equal("sig", key.GetProperty("use").GetString());
        Assert.False(string.IsNullOrEmpty(key.GetProperty("kid").GetString()));
    }

    [Fact]
    public async Task TweeAanvragenGebruikenDezelfdeSleutel()
    {
        await using var db = await LtiDatabase.CreateAsync(Ct);

        var first = (await db.KeyService().JwksAsync(Ct)).Kids();
        var second = (await db.KeyService().JwksAsync(Ct)).Kids();
        var signing = await db.KeyService().ActiveAsync(Ct);

        Assert.Equal(first, second);
        Assert.Equal(first[0], signing.KeyId);
    }

    [Fact]
    public async Task GelijktijdigeEersteAanvragenMakenEenActieveSleutel()
    {
        await using var db = await LtiDatabase.CreateAsync(Ct);

        var kids = await Task.WhenAll(Enumerable.Range(0, 4).Select(_ => Task.Run(async () => (await db.KeyService().ActiveAsync(Ct)).KeyId, Ct)));

        Assert.Single(kids.Distinct());
        Assert.Single((await db.KeyService().JwksAsync(Ct)).Kids());
    }

    [Fact]
    public async Task JwksBevatAlleenPubliekeSleutelgegevens()
    {
        await using var db = await LtiDatabase.CreateAsync(Ct);

        var key = (await db.KeyService().JwksAsync(Ct)).GetProperty("keys")[0];

        var members = key.EnumerateObject().Select(p => p.Name).Order().ToArray();
        Assert.Equal(["alg", "e", "kid", "kty", "n", "use"], members);
    }

    [Fact]
    public async Task PrivesleutelStaatVersleuteldInDeDatabase()
    {
        await using var db = await LtiDatabase.CreateAsync(Ct);
        await db.KeyService().ActiveAsync(Ct);

        var stored = (await db.CreateContext().Keys.SingleAsync(Ct)).PrivateKey;

        using var rsa = RSA.Create();
        Assert.ThrowsAny<CryptographicException>(() => rsa.ImportPkcs8PrivateKey(stored, out _));
    }

    [Fact]
    public async Task RoterenMaaktEenNieuweActieveSleutel()
    {
        await using var db = await LtiDatabase.CreateAsync(Ct);
        var first = (await db.KeyService().ActiveAsync(Ct)).KeyId;

        await db.KeyService().RotateAsync(Ct);

        var jwks = await db.KeyService().JwksAsync(Ct);
        var active = (await db.KeyService().ActiveAsync(Ct)).KeyId;
        Assert.Equal(2, jwks.Kids().Length);
        Assert.NotEqual(first, active);
        Assert.Equal(active, jwks.Kids()[0]); // the active key first
        Assert.Contains(first, jwks.Kids());
    }

    [Fact]
    public async Task OudeSleutelVerdwijntNaDeBewaartermijn()
    {
        await using var db = await LtiDatabase.CreateAsync(Ct);
        var time = new FakeTimeProvider();
        var first = (await db.KeyService(time).ActiveAsync(Ct)).KeyId;
        await db.KeyService(time).RotateAsync(Ct);

        time.Advance(TimeSpan.FromDays(6));
        Assert.Equal(2, (await db.KeyService(time).JwksAsync(Ct)).Kids().Length);

        time.Advance(TimeSpan.FromDays(2));
        var jwks = await db.KeyService(time).JwksAsync(Ct);

        Assert.Single(jwks.Kids());
        Assert.DoesNotContain(first, jwks.Kids());
    }

    [Fact]
    public async Task OnleesbarePrivesleutelWordtVervangen()
    {
        await using var db = await LtiDatabase.CreateAsync(Ct);
        var lost = (await db.KeyService(ring: "ring-a").ActiveAsync(Ct)).KeyId;

        // The same database, but another Data Protection key ring: the stored private key can't be decrypted any more.
        var signing = await db.KeyService(ring: "ring-b").ActiveAsync(Ct);
        var jwks = await db.KeyService(ring: "ring-b").JwksAsync(Ct);

        Assert.NotEqual(lost, signing.KeyId);
        Assert.Equal([signing.KeyId], jwks.Kids());
    }
}
