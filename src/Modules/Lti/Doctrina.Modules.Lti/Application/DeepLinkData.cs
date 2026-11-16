using System.Globalization;
using System.Security.Cryptography;
using Microsoft.AspNetCore.DataProtection;

namespace Doctrina.Modules.Lti.Application;

/// <summary>
/// The opaque <c>data</c> of a deep linking request, which the tool returns unchanged in its response (design D2 of
/// add-lti-deep-linking): the section, the tool and the user it was issued for, valid for 15 minutes.
/// </summary>
internal sealed class DeepLinkData(IDataProtectionProvider dataProtection, TimeProvider time)
{
    public const string Purpose = "Lti.DeepLinking.Data";

    public static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(15);

    private IDataProtector Protector => dataProtection.CreateProtector(Purpose);

    public string Protect(Guid sectionId, Guid toolId, string subject) =>
        Protector.Protect(string.Create(CultureInfo.InvariantCulture, $"{sectionId:N}.{toolId:N}.{subject}.{time.GetUtcNow().Add(Lifetime).ToUnixTimeSeconds()}"));

    /// <summary>The section, tool and user of unexpired data of ours, or <see langword="null"/>.</summary>
    public (Guid SectionId, Guid ToolId, string Subject)? Open(string? data)
    {
        if (string.IsNullOrEmpty(data))
        {
            return null;
        }

        try
        {
            var parts = Protector.Unprotect(data).Split('.');
            return parts.Length == 4
                && Guid.TryParseExact(parts[0], "N", out var section)
                && Guid.TryParseExact(parts[1], "N", out var tool)
                && long.TryParse(parts[3], NumberStyles.None, CultureInfo.InvariantCulture, out var expires)
                && time.GetUtcNow().ToUnixTimeSeconds() < expires
                    ? (section, tool, parts[2])
                    : null;
        }
        catch (CryptographicException)
        {
            return null;
        }
    }
}
