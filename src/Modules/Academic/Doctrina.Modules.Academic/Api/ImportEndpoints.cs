using System.Security.Claims;
using Doctrina.Core.Results;
using Doctrina.Infrastructure.Api;
using Doctrina.Infrastructure.Authentication;
using Doctrina.Infrastructure.Authorization;
using Doctrina.Modules.Academic.Application;
using Doctrina.Modules.Academic.Domain;
using Doctrina.Modules.Academic.Infrastructure;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace Doctrina.Modules.Academic.Api;

/// <summary>Import jobs through the API (change add-bulk-import, design D8). All need <c>Academic.Import</c>.</summary>
internal static class ImportEndpoints
{
    public const int MaxReportPageSize = 1000;

    public static void Map(IEndpointRouteBuilder api)
    {
        var imports = api.MapGroup("imports").WithTags("imports").RequirePermission(AcademicPermissions.Import);

        imports.MapPost("", CreateAsync)
            .DisableAntiforgery() // bearer API, not a form in a browser
            .RequireRateLimiting(ApiRateLimits.Batch)
            .WithSummary("Upload a CSV (with type) or a zip and start an import")
            .WithDescription("Multipart: file (CSV or zip), type (for a CSV). Query: dryRun=true to only see what would happen. Answers 202 with the job.");
        imports.MapGet("{id:guid}", GetAsync).WithSummary("Status and summary of an import");
        imports.MapGet("{id:guid}/report", ReportAsync)
            .WithSummary("The result per row")
            .WithDescription("Query: pageNumber, pageSize (at most 1000), or format=csv for the whole report as CSV.");
        imports.MapGet("templates/{type}.csv", (string type) => AcademicTypes.Find(type) is { } t
                ? Results.Text(ImportReportCsv.Template(t), "text/csv")
                : Result.Failure(ImportUpload.UnknownType(type)).ToHttpResult())
            .WithSummary("An empty CSV template for a type");
    }

    private static async Task<IResult> CreateAsync(
        IFormFile file, [FromForm] string? type, bool? dryRun, ImportJobs jobs, ClaimsPrincipal user, IConfiguration config, CancellationToken ct)
    {
        using var buffer = new MemoryStream();
        await file.CopyToAsync(buffer, ct);
        var client = user.FindFirst(DoctrinaClaimTypes.AuthorizedParty)?.Value ?? user.Identity?.Name ?? "(unknown)";
        var result = await jobs.CreateAsync(buffer.ToArray(), file.FileName, type, ApiSource.For(user, config), dryRun == true, client, ct);
        return result.ToHttpResult(job => Results.Accepted($"/api/v1/imports/{job.Id:D}", Representation(job)));
    }

    private static async Task<IResult> GetAsync(Guid id, AcademicDbContext db, CancellationToken ct) =>
        await db.ImportJobs.AsNoTracking().SingleOrDefaultAsync(j => j.Id == id, ct) is { } job
            ? Results.Ok(Representation(job))
            : NotFound(id);

    private static async Task<IResult> ReportAsync(Guid id, string? format, int? pageNumber, int? pageSize, AcademicDbContext db, CancellationToken ct)
    {
        if (!await db.ImportJobs.AnyAsync(j => j.Id == id, ct))
        {
            return NotFound(id);
        }

        var rows = db.ImportJobRows.AsNoTracking().Where(r => r.ImportJobId == id).OrderBy(r => r.File).ThenBy(r => r.Line);
        if (string.Equals(format, "csv", StringComparison.OrdinalIgnoreCase))
        {
            var all = await rows.ToListAsync(ct);
            return Results.File(ImportReportCsv.Encode(ImportReportCsv.Header, all.Select(ImportReportCsv.Line)), "text/csv", $"import-{id:N}-report.csv");
        }

        var (number, size) = (pageNumber ?? 1, pageSize ?? AcademicEndpoints.DefaultPageSize);
        if (number < 1 || size < 1 || size > MaxReportPageSize)
        {
            return Result.Failure(Error.Validation(AcademicApiErrors.InvalidQuery, "The query is invalid.",
                new Dictionary<string, string[]> { ["pageSize"] = [$"pageNumber from 1, pageSize from 1 to {MaxReportPageSize}."] })).ToHttpResult();
        }

        var total = await rows.CountAsync(ct);
        var totalPages = (total + size - 1) / size;
        var items = await rows.Skip((number - 1) * size).Take(size).ToListAsync(ct);
        return Results.Ok(new
        {
            pageSize = size,
            pageNumber = number,
            totalPages,
            hasPreviousPage = number > 1,
            hasNextPage = number < totalPages,
            items = items.Select(r => new { file = r.File, line = r.Line, key = r.Key, result = r.Outcome.ToString().ToLowerInvariant(), code = r.Code, message = r.Message }),
        });
    }

    private static object Representation(ImportJob job) => new
    {
        id = job.Id,
        status = job.Status.ToString().ToLowerInvariant(),
        dryRun = job.DryRun,
        source = job.Source,
        file = job.UploadName,
        type = job.UploadType,
        startedBy = job.StartedBy,
        createdAt = job.CreatedAt,
        startedAt = job.StartedAt,
        finishedAt = job.FinishedAt,
        created = job.Created,
        updated = job.Updated,
        unchanged = job.Unchanged,
        failed = job.Failed,
        message = job.Message,
        warnings = job.Warnings?.Split('\n'),
    };

    private static IResult NotFound(Guid id) =>
        Result.Failure(Error.NotFound(ImportErrors.NotFound, $"Import {id} doesn't exist.")).ToHttpResult();
}
