using Doctrina.Modules.Lti.Domain;
using Doctrina.Modules.Lti.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Doctrina.Modules.Lti.Application;

/// <summary>Remembers the nonces and token ids tools used, so a signed message can only be used once.</summary>
internal static class NonceStore
{
    /// <summary>
    /// Records the value and removes the expired ones; <see langword="false"/> if the tool used it before for this
    /// purpose (the unique index decides). The value is kept until five minutes after the message expires.
    /// </summary>
    public static async Task<bool> UseAsync(LtiDbContext db, string purpose, Guid toolId, string nonce, DateTime validTo, DateTimeOffset now, CancellationToken ct)
    {
        await db.Nonces.Where(n => n.Expires < now).ExecuteDeleteAsync(ct);
        db.Add(new JwtNonce { ToolId = toolId, Purpose = purpose, Nonce = nonce, Expires = new DateTimeOffset(validTo, TimeSpan.Zero).AddMinutes(5) });
        try
        {
            await db.SaveChangesAsync(ct);
            return true;
        }
        catch (DbUpdateException)
        {
            db.ChangeTracker.Clear();
            return false;
        }
    }
}
