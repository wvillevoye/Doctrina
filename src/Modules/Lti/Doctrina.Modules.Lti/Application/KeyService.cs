using System.Security.Cryptography;
using System.Text.Json;
using Doctrina.Core.Entities;
using Doctrina.Modules.Lti.Domain;
using Doctrina.Modules.Lti.Infrastructure;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Doctrina.Modules.Lti.Application;

/// <summary>Settings of the Lti module (<c>Lti</c> in appsettings.json).</summary>
internal sealed class LtiOptions
{
    public const string Section = "Lti";

    /// <summary>The stable address of this platform (<c>iss</c> of launches); empty = the address of the request.</summary>
    public string? Issuer { get; set; }

    public LtiKeyOptions Keys { get; set; } = new();
}

internal sealed class LtiKeyOptions
{
    /// <summary>How long a rotated key stays in the JWKS, so tools can refresh their cache.</summary>
    public int RetireAfterDays { get; set; } = 7;
}

/// <summary>
/// The platform's RSA signing keys (design D7 of add-lti-platform): the active key, rotating it, and the public JWKS.
/// The private key is encrypted with Data Protection before it reaches the database.
/// </summary>
internal sealed class KeyService(LtiDbContext db, IDataProtectionProvider dataProtection, TimeProvider time, IOptions<LtiOptions> options)
{
    public const string Purpose = "Lti.Keys";
    private const int KeySize = 2048;

    private IDataProtector Protector => dataProtection.CreateProtector(Purpose);

    /// <summary>The active key with its private part, made when there is none or when it can't be decrypted.</summary>
    public async Task<RsaSecurityKey> ActiveAsync(CancellationToken ct)
    {
        for (var attempt = 0; attempt < 3; attempt++)
        {
            var active = await db.Keys.FirstOrDefaultAsync(k => k.Active && k.Status == EntityStatus.Active, ct)
                ?? await TryCreateAsync(ct);
            if (active is not null && TryOpen(active, out var key))
            {
                return key;
            }

            if (active is not null)
            {
                // Data Protection key ring lost or changed: this key is useless, so replace it (design D7).
                active.Active = false;
                active.Status = EntityStatus.ToBeDeleted;
                await db.SaveChangesAsync(ct);
            }
        }

        throw new InvalidOperationException("No usable LTI signing key could be made.");
    }

    /// <summary>Retires the active key (it stays in the JWKS for a while) and makes a new active one.</summary>
    public async Task RotateAsync(CancellationToken ct)
    {
        var active = await db.Keys.FirstOrDefaultAsync(k => k.Active && k.Status == EntityStatus.Active, ct);
        if (active is not null)
        {
            active.Active = false;
            active.RetiredAt = time.GetUtcNow();
            await db.SaveChangesAsync(ct);
        }

        await ActiveAsync(ct);
    }

    /// <summary>The public keys as a JWK set: the active key first, then keys retired less than the retention time ago.</summary>
    public async Task<JsonElement> JwksAsync(CancellationToken ct)
    {
        await ActiveAsync(ct); // there is always at least the active key
        var limit = time.GetUtcNow().AddDays(-Math.Max(0, options.Value.Keys.RetireAfterDays));
        var keys = await db.Keys.Where(k => k.Status == EntityStatus.Active).ToListAsync(ct);
        foreach (var expired in keys.Where(k => !k.Active && k.RetiredAt <= limit))
        {
            expired.Status = EntityStatus.ToBeDeleted;
        }

        await db.SaveChangesAsync(ct);
        var published = keys.Where(k => k.Status == EntityStatus.Active).OrderByDescending(k => k.Active).ThenByDescending(k => k.RetiredAt);
        return JsonSerializer.SerializeToElement(new
        {
            keys = published.Select(k =>
            {
                using var pub = JsonDocument.Parse(k.PublicKey);
                return new Dictionary<string, string>
                {
                    ["kty"] = "RSA",
                    ["use"] = "sig",
                    ["alg"] = "RS256",
                    ["kid"] = k.Kid,
                    ["n"] = pub.RootElement.GetProperty("n").GetString()!,
                    ["e"] = pub.RootElement.GetProperty("e").GetString()!,
                };
            }),
        });
    }

    /// <summary>The public keys that may have signed a token: the active key and keys retired less than the retention time ago.</summary>
    public async Task<IReadOnlyList<SecurityKey>> ValidationKeysAsync(CancellationToken ct)
    {
        var limit = time.GetUtcNow().AddDays(-Math.Max(0, options.Value.Keys.RetireAfterDays));
        var rows = await db.Keys.AsNoTracking().Where(k => k.Status == EntityStatus.Active && (k.Active || k.RetiredAt > limit)).ToListAsync(ct);
        return [.. rows.Select(k =>
        {
            using var pub = JsonDocument.Parse(k.PublicKey);
            var parameters = new RSAParameters
            {
                Modulus = WebEncoders.Base64UrlDecode(pub.RootElement.GetProperty("n").GetString()!),
                Exponent = WebEncoders.Base64UrlDecode(pub.RootElement.GetProperty("e").GetString()!),
            };
            return (SecurityKey)new RsaSecurityKey(parameters) { KeyId = k.Kid };
        })];
    }

    /// <summary>The Kid of the active key, for the admin page.</summary>
    public async Task<string?> ActiveKidAsync(CancellationToken ct) =>
        (await db.Keys.AsNoTracking().FirstOrDefaultAsync(k => k.Active && k.Status == EntityStatus.Active, ct))?.Kid;

    private async Task<LtiKey?> TryCreateAsync(CancellationToken ct)
    {
        using var rsa = RSA.Create(KeySize);
        var parameters = rsa.ExportParameters(includePrivateParameters: false);
        var key = new LtiKey
        {
            ExternalId = string.Empty,
            Source = ToolService.DoctrinaSource,
            Kid = WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(12)),
            PrivateKey = Protector.Protect(rsa.ExportPkcs8PrivateKey()),
            PublicKey = JsonSerializer.Serialize(new { n = WebEncoders.Base64UrlEncode(parameters.Modulus!), e = WebEncoders.Base64UrlEncode(parameters.Exponent!) }),
            Active = true,
        };
        db.Add(key);
        key.ExternalId = key.Id.ToString("D");
        try
        {
            await db.SaveChangesAsync(ct);
            return key;
        }
        catch (DbUpdateException)
        {
            // Another request made the active key at the same moment (unique index): use that one.
            db.Entry(key).State = EntityState.Detached;
            return await db.Keys.FirstOrDefaultAsync(k => k.Active && k.Status == EntityStatus.Active, ct);
        }
    }

    private bool TryOpen(LtiKey key, out RsaSecurityKey result)
    {
        try
        {
            var rsa = RSA.Create();
            rsa.ImportPkcs8PrivateKey(Protector.Unprotect(key.PrivateKey), out _);
            result = new RsaSecurityKey(rsa) { KeyId = key.Kid };
            return true;
        }
        catch (CryptographicException)
        {
            result = null!;
            return false;
        }
    }
}

/// <summary>Facts about this platform that tools need (design D9).</summary>
internal static class LtiPlatform
{
    /// <summary>The configured issuer, or else the address the request came in on (<paramref name="baseUri"/>), without a trailing slash.</summary>
    public static string Issuer(LtiOptions options, string baseUri) =>
        (string.IsNullOrWhiteSpace(options.Issuer) ? baseUri : options.Issuer).Trim().TrimEnd('/');
}
