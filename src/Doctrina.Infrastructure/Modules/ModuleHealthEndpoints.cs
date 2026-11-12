using System.Text.Json;
using Doctrina.Core.Modules;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Doctrina.Infrastructure.Modules;

/// <summary>
/// Readiness-endpoint dat de health checks per module groepeert (spec module-system "Modules melden hun gezondheid").
/// Toont alleen namen en statussen: geen omschrijvingen, exceptions, connection strings of persoonsgegevens.
/// </summary>
public static class ModuleHealthEndpoints
{
    public const string LivePath = "/health/live";
    public const string ReadyPath = "/health/ready";

    /// <summary>
    /// <c>/health/live</c> (geen checks: gezond zolang het proces draait) en <c>/health/ready</c> (database + checks van
    /// alle ingeschakelde modules), beide zonder authenticatie (spec api-conventions "Liveness- en readiness-endpoints").
    /// </summary>
    public static IEndpointRouteBuilder MapDoctrinaHealth(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapHealthChecks(LivePath, new HealthCheckOptions { Predicate = _ => false, ResponseWriter = WriteReportAsync })
            .AllowAnonymous();
        endpoints.MapHealthChecks(ReadyPath, new HealthCheckOptions { ResponseWriter = WriteReportAsync })
            .AllowAnonymous();
        return endpoints;
    }

    private static Task WriteReportAsync(HttpContext context, HealthReport report)
    {
        var modules = new SortedDictionary<string, ModuleHealth>(StringComparer.Ordinal);
        var other = new SortedDictionary<string, string>(StringComparer.Ordinal);

        foreach (var (name, entry) in report.Entries)
        {
            var module = entry.Tags
                .FirstOrDefault(t => t.StartsWith(ModuleHealthChecks.Tag + ":", StringComparison.Ordinal))?[(ModuleHealthChecks.Tag.Length + 1)..];
            if (module is null)
            {
                other[name] = entry.Status.ToString();
                continue;
            }

            if (!modules.TryGetValue(module, out var health))
            {
                health = new ModuleHealth();
                modules[module] = health;
            }

            health.Checks[name] = entry.Status.ToString();
            if (entry.Status < health.WorstStatus)
            {
                health.WorstStatus = entry.Status;
            }
        }

        context.Response.ContentType = "application/json";
        return JsonSerializer.SerializeAsync(
            context.Response.Body,
            new HealthResponse(
                report.Status.ToString(),
                modules.ToDictionary(m => m.Key, m => new ModuleHealthResponse(m.Value.WorstStatus.ToString(), m.Value.Checks), StringComparer.Ordinal),
                other),
            HealthJson.Options,
            context.RequestAborted);
    }

    private sealed class ModuleHealth
    {
        public HealthStatus WorstStatus { get; set; } = HealthStatus.Healthy;

        public SortedDictionary<string, string> Checks { get; } = new(StringComparer.Ordinal);
    }

    private static class HealthJson
    {
        public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);
    }
}

/// <summary>Antwoord van <c>/health/ready</c>.</summary>
public sealed record HealthResponse(
    string Status,
    IReadOnlyDictionary<string, ModuleHealthResponse> Modules,
    IReadOnlyDictionary<string, string> Checks);

/// <summary>Gezondheid van één module.</summary>
public sealed record ModuleHealthResponse(string Status, IReadOnlyDictionary<string, string> Checks);
