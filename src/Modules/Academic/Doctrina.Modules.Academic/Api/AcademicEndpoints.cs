using System.Security.Claims;
using System.Text.Json.Nodes;
using Doctrina.Core.Entities;
using Doctrina.Core.Results;
using Doctrina.Infrastructure.Api;
using Doctrina.Infrastructure.Authorization;
using Doctrina.Modules.Academic.Application;
using Doctrina.Modules.Academic.Infrastructure;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace Doctrina.Modules.Academic.Api;

/// <summary>Error codes of the Academic API that don't belong to one type.</summary>
internal static class AcademicApiErrors
{
    public const string KeyMismatch = "Academic.Api.KeyMismatch";
    public const string InvalidQuery = "Academic.Api.InvalidQuery";
    public const string BatchTooLarge = "Academic.Api.BatchTooLarge";
    public const string OtherSource = "Academic.Record.OtherSource";

    public static readonly string[] All = [KeyMismatch, InvalidQuery, BatchTooLarge, OtherSource];
}

/// <summary>
/// REST endpoints for every <see cref="AcademicType"/> under <c>/api/v1/{type}</c> (change add-rest-api, design D2–D7):
/// the same seven endpoints for all six types, through the same validation and upsert as the CSV import.
/// </summary>
internal static class AcademicEndpoints
{
    public const int DefaultPageSize = 100;
    public const int MaxPageSize = 500;
    public const int MaxBatch = 500;

    public static void Map(IEndpointRouteBuilder api)
    {
        foreach (var type in AcademicTypes.All)
        {
            var group = api.MapGroup(type.Name).WithTags(type.Name);
            var fields = FieldList(type);

            group.MapGet("", (AcademicDbContext db, HttpContext http, CancellationToken ct) => ListAsync(type, db, http.Request.Query, ct))
                .RequirePermission(AcademicPermissions.Read)
                .WithSummary($"List {type.Name}")
                .WithDescription("Query: pageNumber (from 1), pageSize (1–500, default 100), source, status (active, toBeDeleted), modifiedSince (ISO 8601).");
            group.MapGet("{id:guid}", (Guid id, AcademicDbContext db, CancellationToken ct) => GetAsync(type, db, id, ct))
                .RequirePermission(AcademicPermissions.Read)
                .WithSummary($"One record of {type.Name} by Doctrina id");
            group.MapPost("", (JsonObject body, AcademicDbContext db, ClaimsPrincipal user, IConfiguration config, CancellationToken ct) =>
                    CreateAsync(type, db, body, ApiSource.For(user, config), ct))
                .RequirePermission(AcademicPermissions.Write)
                .WithSummary($"Create a record of {type.Name}")
                .WithDescription($"Fields: {fields}. 409 if the {type.KeyField} already exists for your source.");
            group.MapPut("by-external-id/{externalId}", (string externalId, JsonObject body, AcademicDbContext db, ClaimsPrincipal user, IConfiguration config, CancellationToken ct) =>
                    UpsertAsync(type, db, externalId, body, ApiSource.For(user, config), ct))
                .RequirePermission(AcademicPermissions.Write)
                .WithSummary($"Create or update a record of {type.Name} by the key of the source system")
                .WithDescription($"Fields: {fields}. 201 when created, 200 when updated or unchanged.");
            group.MapPatch("{id:guid}", (Guid id, JsonObject body, AcademicDbContext db, ClaimsPrincipal user, IConfiguration config, CancellationToken ct) =>
                    PatchAsync(type, db, id, body, ApiSource.For(user, config), ct))
                .RequirePermission(AcademicPermissions.Write)
                .WithSummary($"Change some fields of a record of {type.Name}")
                .WithDescription($"Fields: {fields}. Fields that are left out keep their value; null empties a field.");
            group.MapDelete("{id:guid}", (Guid id, AcademicDbContext db, ClaimsPrincipal user, IConfiguration config, CancellationToken ct) =>
                    DeleteAsync(type, db, id, ApiSource.For(user, config), ct))
                .RequirePermission(AcademicPermissions.Write)
                .WithSummary($"Mark a record of {type.Name} as to be deleted");
            group.MapPost("batch", (JsonArray body, AcademicDbContext db, ClaimsPrincipal user, IConfiguration config, CancellationToken ct) =>
                    BatchAsync(type, db, body, ApiSource.For(user, config), ct))
                .RequirePermission(AcademicPermissions.Write)
                .RequireRateLimiting(ApiRateLimits.Batch)
                .WithSummary($"Create or update up to {MaxBatch} records of {type.Name}")
                .WithDescription($"An array of records with fields: {fields}. The result per record is in the same order.");
        }
    }

    // ponytail: field list in the description; a schema per type (OpenAPI transformer from AcademicType.Fields) when partners need it.
    private static string FieldList(AcademicType type) =>
        string.Join(", ", type.Fields.Select(f => f.Required ? $"{f.Name} (required)" : f.Name));

    private static async Task<IResult> ListAsync(AcademicType type, AcademicDbContext db, IQueryCollection query, CancellationToken ct)
    {
        var errors = new Dictionary<string, string[]>(StringComparer.Ordinal);
        var pageNumber = Number(query, "pageNumber", 1, 1, int.MaxValue, errors);
        var pageSize = Number(query, "pageSize", DefaultPageSize, 1, MaxPageSize, errors);
        EntityStatus? status = query["status"].ToString() switch
        {
            "" => null,
            "active" => EntityStatus.Active,
            "toBeDeleted" => EntityStatus.ToBeDeleted,
            _ => Invalid(errors, "status", "Must be active or toBeDeleted."),
        };
        DateTimeOffset? modifiedSince = null;
        if (query["modifiedSince"].ToString() is { Length: > 0 } since)
        {
            if (DateTimeOffset.TryParse(since, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.AssumeUniversal, out var parsed))
            {
                modifiedSince = parsed;
            }
            else
            {
                errors["modifiedSince"] = ["Must be an ISO 8601 date and time."];
            }
        }

        if (errors.Count > 0)
        {
            return Result.Failure(Error.Validation(AcademicApiErrors.InvalidQuery, "The query is invalid.", errors)).ToHttpResult();
        }

        var records = type.Query(db).AsNoTracking();
        if (query["source"].ToString() is { Length: > 0 } source)
        {
            records = records.Where(e => e.Source == source);
        }

        if (status is { } s)
        {
            records = records.Where(e => e.Status == s);
        }

        if (modifiedSince is { } m)
        {
            records = records.Where(e => e.DateLastModified >= m);
        }

        var total = await records.CountAsync(ct);
        var totalPages = (total + pageSize - 1) / pageSize;
        var items = await records.OrderBy(e => e.DateLastModified).ThenBy(e => e.Id)
            .Skip((pageNumber - 1) * pageSize).Take(pageSize).ToListAsync(ct);
        return Results.Ok(new
        {
            pageSize,
            pageNumber,
            totalPages,
            hasPreviousPage = pageNumber > 1,
            hasNextPage = pageNumber < totalPages,
            items = items.Select(e => Representation(type, e)),
        });
    }

    private static async Task<IResult> GetAsync(AcademicType type, AcademicDbContext db, Guid id, CancellationToken ct) =>
        await FindAsync(type, db, id, ct) is { } record
            ? Results.Ok(Representation(type, record))
            : NotFound(type, id).ToHttpResult();

    private static async Task<IResult> CreateAsync(AcademicType type, AcademicDbContext db, JsonObject body, string source, CancellationToken ct)
    {
        var fields = ApiInput.ToFields(body, type);
        if (fields.IsFailure)
        {
            return fields.ToHttpResult();
        }

        var key = fields.Value!.GetValueOrDefault(type.KeyField)?.Trim();
        if (!string.IsNullOrEmpty(key) && await type.Query(db).AnyAsync(e => e.Source == source && e.ExternalId == key, ct))
        {
            return Result.Failure(Error.Conflict(type.DuplicateExternalIdCode, $"A {type.EntityName} '{key}' from source '{source}' already exists.")).ToHttpResult();
        }

        return await SaveAsync(type, db, fields.Value!, source, ct);
    }

    private static async Task<IResult> UpsertAsync(AcademicType type, AcademicDbContext db, string externalId, JsonObject body, string source, CancellationToken ct)
    {
        var fields = ApiInput.ToFields(body, type);
        if (fields.IsFailure)
        {
            return fields.ToHttpResult();
        }

        if (KeyMismatch(type, fields.Value!, externalId) is { } mismatch)
        {
            return Result.Failure(mismatch).ToHttpResult();
        }

        fields.Value![type.KeyField] = externalId;
        return await SaveAsync(type, db, fields.Value, source, ct);
    }

    private static async Task<IResult> PatchAsync(AcademicType type, AcademicDbContext db, Guid id, JsonObject body, string source, CancellationToken ct)
    {
        var record = await FindAsync(type, db, id, ct);
        if (record is null)
        {
            return NotFound(type, id).ToHttpResult();
        }

        if (record.Source != source)
        {
            return OtherSource(type, record).ToHttpResult();
        }

        var changes = ApiInput.ToFields(body, type);
        if (changes.IsFailure)
        {
            return changes.ToHttpResult();
        }

        if (KeyMismatch(type, changes.Value!, record.ExternalId) is { } mismatch)
        {
            return Result.Failure(mismatch).ToHttpResult();
        }

        var fields = type.ToFields(record);
        foreach (var (name, value) in changes.Value!)
        {
            fields[name] = value;
        }

        return await SaveAsync(type, db, fields, source, ct);
    }

    private static async Task<IResult> DeleteAsync(AcademicType type, AcademicDbContext db, Guid id, string source, CancellationToken ct)
    {
        var record = await type.Query(db).SingleOrDefaultAsync(e => e.Id == id, ct);
        if (record is null)
        {
            return NotFound(type, id).ToHttpResult();
        }

        if (record.Source != source)
        {
            return OtherSource(type, record).ToHttpResult();
        }

        record.Status = EntityStatus.ToBeDeleted;
        await db.SaveChangesAsync(ct);
        return Results.NoContent();
    }

    private static async Task<IResult> BatchAsync(AcademicType type, AcademicDbContext db, JsonArray body, string source, CancellationToken ct)
    {
        if (body.Count > MaxBatch)
        {
            return Result.Failure(Error.Validation(AcademicApiErrors.BatchTooLarge, $"A batch has at most {MaxBatch} records; this one has {body.Count}.",
                new Dictionary<string, string[]> { ["batch"] = [$"At most {MaxBatch} records."] })).ToHttpResult();
        }

        var results = new List<Dictionary<string, object?>>(body.Count);
        for (var index = 0; index < body.Count; index++)
        {
            var item = new Dictionary<string, object?> { ["index"] = index };
            Result<UpsertOutcome> outcome;
            if (body[index] is JsonObject element)
            {
                var fields = ApiInput.ToFields(element, type);
                item[type.KeyField] = fields.Value?.GetValueOrDefault(type.KeyField);
                outcome = fields.IsFailure ? fields.Error! : await type.UpsertAsync(db, source, fields.Value!, ct);
            }
            else
            {
                outcome = Error.Validation(type.InvalidCode, "Each record must be a JSON object.");
            }

            if (outcome.IsSuccess)
            {
                item["result"] = outcome.Value.ToString().ToLowerInvariant();
            }
            else
            {
                item["result"] = "failed";
                item["code"] = outcome.Error!.Code;
                item["detail"] = outcome.Error.Message;
                item["errors"] = outcome.Error.Details;
                db.ChangeTracker.Clear();
            }

            results.Add(item);
        }

        return Results.Ok(results);
    }

    private static async Task<IResult> SaveAsync(AcademicType type, AcademicDbContext db, Dictionary<string, string?> fields, string source, CancellationToken ct)
    {
        var outcome = await type.UpsertAsync(db, source, fields, ct);
        if (outcome.IsFailure)
        {
            return outcome.ToHttpResult();
        }

        var key = fields[type.KeyField]!.Trim();
        var record = await type.Query(db).AsNoTracking().SingleAsync(e => e.Source == source && e.ExternalId == key, ct);
        var representation = Representation(type, record);
        return outcome.Value == UpsertOutcome.Created
            ? Results.Created($"/api/v1/{type.Name}/{record.Id:D}", representation)
            : Results.Ok(representation);
    }

    private static Task<ExchangeableEntity?> FindAsync(AcademicType type, AcademicDbContext db, Guid id, CancellationToken ct) =>
        type.Query(db).AsNoTracking().SingleOrDefaultAsync(e => e.Id == id, ct);

    /// <summary><c>{ id, …fields, source, status, dateLastModified }</c> (design D2).</summary>
    internal static Dictionary<string, object?> Representation(AcademicType type, ExchangeableEntity record)
    {
        var result = new Dictionary<string, object?> { ["id"] = record.Id };
        foreach (var (name, value) in type.ToFields(record))
        {
            result[name] = value;
        }

        result["source"] = record.Source;
        result["dateLastModified"] = record.DateLastModified.ToUniversalTime();
        return result;
    }

    private static Error? KeyMismatch(AcademicType type, Dictionary<string, string?> fields, string expected) =>
        fields.TryGetValue(type.KeyField, out var key) && key is not null && key.Trim() != expected
            ? Error.Validation(AcademicApiErrors.KeyMismatch, $"{type.KeyField} can't be changed.",
                new Dictionary<string, string[]> { [type.KeyField] = [$"Must be '{expected}' (or left out)."] })
            : null;

    private static Result NotFound(AcademicType type, Guid id) =>
        Error.NotFound(type.NotFoundCode, $"{type.EntityName} {id} doesn't exist.");

    private static Result OtherSource(AcademicType type, ExchangeableEntity record) =>
        Error.Forbidden(AcademicApiErrors.OtherSource, $"This {type.EntityName} belongs to source '{record.Source}'; you can only change records of your own source.");

    private static int Number(IQueryCollection query, string name, int fallback, int min, int max, Dictionary<string, string[]> errors)
    {
        var text = query[name].ToString();
        if (text.Length == 0)
        {
            return fallback;
        }

        if (int.TryParse(text, System.Globalization.CultureInfo.InvariantCulture, out var value) && value >= min && value <= max)
        {
            return value;
        }

        errors[name] = [$"Must be a whole number from {min} to {max}."];
        return fallback;
    }

    private static EntityStatus? Invalid(Dictionary<string, string[]> errors, string name, string message)
    {
        errors[name] = [message];
        return null;
    }
}
