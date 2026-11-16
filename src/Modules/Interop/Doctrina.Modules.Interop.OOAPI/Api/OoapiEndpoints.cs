using System.Globalization;
using Doctrina.Infrastructure.Authorization;
using Doctrina.Modules.Academic.Contracts;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Options;

namespace Doctrina.Modules.Interop.OOAPI.Api;

/// <summary>
/// The OOAPI v5 paths Doctrina has data for (design D5, D6), read-only. Nothing is logged here: responses hold personal data
/// and a <c>primaryCode</c> can be a person number (design D8).
/// </summary>
internal static class OoapiEndpoints
{
    private static readonly int[] PageSizes = [10, 20, 50, 100, 250];

    public static void Map(IEndpointRouteBuilder ooapi)
    {
        var g = ooapi.MapGroup(string.Empty).RequirePermission(OoapiPermissions.Read);

        g.MapGet(string.Empty, (IOptions<OoapiOptions> o) => Results.Json(new
        {
            contactEmail = o.Value.ContactEmail,
            specification = o.Value.Specification,
            documentation = o.Value.Documentation,
        }));

        g.MapGet("organizations", (string? primaryCode, int? pageNumber, int? pageSize, IAcademicExchangeReader r, IOptions<OoapiOptions> o, CancellationToken ct) =>
            ListAsync(pageNumber, pageSize, (n, s) => r.OrganizationsAsync(primaryCode, n, s, ct), x => Mapping.Organization(x, o.Value)));
        g.MapGet("organizations/{id:guid}", (Guid id, IAcademicExchangeReader r, IOptions<OoapiOptions> o, CancellationToken ct) =>
            OneAsync(() => r.OrganizationAsync(id, ct), x => Mapping.Organization(x, o.Value)));
        g.MapGet("organizations/{id:guid}/offerings", async (Guid id, int? pageNumber, int? pageSize, IAcademicExchangeReader r, IOptions<OoapiOptions> o, CancellationToken ct) =>
            await r.OrganizationAsync(id, ct) is null ? NotFound() : await ListAsync(pageNumber, pageSize, (n, s) => r.OfferingsAsync(null, null, id, n, s, ct), x => Mapping.Offering(x, o.Value)));

        g.MapGet("academic-sessions", (string? primaryCode, int? pageNumber, int? pageSize, IAcademicExchangeReader r, IOptions<OoapiOptions> o, CancellationToken ct) =>
            ListAsync(pageNumber, pageSize, (n, s) => r.SessionsAsync(primaryCode, n, s, ct), x => Mapping.Session(x, o.Value)));
        g.MapGet("academic-sessions/{id:guid}", (Guid id, IAcademicExchangeReader r, IOptions<OoapiOptions> o, CancellationToken ct) =>
            OneAsync(() => r.SessionAsync(id, ct), x => Mapping.Session(x, o.Value)));
        g.MapGet("academic-sessions/{id:guid}/offerings", async (Guid id, int? pageNumber, int? pageSize, IAcademicExchangeReader r, IOptions<OoapiOptions> o, CancellationToken ct) =>
            await r.SessionAsync(id, ct) is null ? NotFound() : await ListAsync(pageNumber, pageSize, (n, s) => r.OfferingsAsync(null, id, null, n, s, ct), x => Mapping.Offering(x, o.Value)));

        g.MapGet("courses", (string? primaryCode, int? pageNumber, int? pageSize, IAcademicExchangeReader r, IOptions<OoapiOptions> o, CancellationToken ct) =>
            ListAsync(pageNumber, pageSize, (n, s) => r.CoursesAsync(primaryCode, n, s, ct), x => Mapping.Course(x, o.Value)));
        g.MapGet("courses/{id:guid}", (Guid id, IAcademicExchangeReader r, IOptions<OoapiOptions> o, CancellationToken ct) =>
            OneAsync(() => r.CourseAsync(id, ct), x => Mapping.Course(x, o.Value)));
        g.MapGet("courses/{id:guid}/offerings", async (Guid id, int? pageNumber, int? pageSize, IAcademicExchangeReader r, IOptions<OoapiOptions> o, CancellationToken ct) =>
            await r.CourseAsync(id, ct) is null ? NotFound() : await ListAsync(pageNumber, pageSize, (n, s) => r.OfferingsAsync(id, null, null, n, s, ct), x => Mapping.Offering(x, o.Value)));

        g.MapGet("offerings/{id:guid}", (Guid id, IAcademicExchangeReader r, IOptions<OoapiOptions> o, CancellationToken ct) =>
            OneAsync(() => r.OfferingAsync(id, ct), x => Mapping.Offering(x, o.Value)));
        g.MapGet("offerings/{id:guid}/associations", async (Guid id, string? role, int? pageNumber, int? pageSize, IAcademicExchangeReader r, CancellationToken ct) =>
        {
            if (role is not null && Mapping.ParseRole(role) is null)
            {
                return Problem(StatusCodes.Status400BadRequest, "Unknown role.");
            }

            return await r.OfferingAsync(id, ct) is null
                ? NotFound()
                : await ListAsync(pageNumber, pageSize, (n, s) => r.AssociationsAsync(id, null, Mapping.ParseRole(role), n, s, ct), Mapping.Association);
        });

        g.MapGet("persons", (string? primaryCode, int? pageNumber, int? pageSize, IAcademicExchangeReader r, CancellationToken ct) =>
            ListAsync(pageNumber, pageSize, (n, s) => r.PersonsAsync(primaryCode, n, s, ct), Mapping.Person));
        g.MapGet("persons/{id:guid}", (Guid id, IAcademicExchangeReader r, CancellationToken ct) =>
            OneAsync(() => r.PersonAsync(id, ct), Mapping.Person));
        g.MapGet("persons/{id:guid}/associations", async (Guid id, int? pageNumber, int? pageSize, IAcademicExchangeReader r, CancellationToken ct) =>
            await r.PersonAsync(id, ct) is null ? NotFound() : await ListAsync(pageNumber, pageSize, (n, s) => r.AssociationsAsync(null, id, null, n, s, ct), Mapping.Association));

        g.MapGet("associations/{id:guid}", (Guid id, IAcademicExchangeReader r, CancellationToken ct) =>
            OneAsync(() => r.AssociationAsync(id, ct), Mapping.Association));
    }

    private static async Task<IResult> OneAsync<T>(Func<Task<T?>> read, Func<T, object> map)
        where T : class =>
        await read() is { } item ? Results.Json(map(item)) : NotFound();

    private static async Task<IResult> ListAsync<T>(int? pageNumber, int? pageSize, Func<int, int, Task<ExchangePage<T>>> read, Func<T, object> map)
    {
        var number = pageNumber ?? 1;
        var size = pageSize ?? PageSizes[0];
        if (number < 1 || !PageSizes.Contains(size))
        {
            return Problem(StatusCodes.Status400BadRequest, $"pageNumber starts at 1; pageSize is one of {string.Join(", ", PageSizes)}.");
        }

        var page = await read(number, size);
        return Results.Json(new
        {
            pageSize = size,
            pageNumber = number,
            hasPreviousPage = number > 1,
            hasNextPage = (long)number * size < page.TotalItems,
            totalPages = (page.TotalItems + size - 1) / size,
            items = page.Items.Select(map),
        });
    }

    private static IResult NotFound() => Problem(StatusCodes.Status404NotFound, "Not found.");

    // OOAPI v5 errors: application/problem+json with status (a string) and title.
    private static IResult Problem(int status, string title) =>
        Results.Json(new { status = status.ToString(CultureInfo.InvariantCulture), title }, contentType: "application/problem+json", statusCode: status);
}
