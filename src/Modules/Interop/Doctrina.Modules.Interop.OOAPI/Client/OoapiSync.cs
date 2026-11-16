using System.Collections.Concurrent;
using Doctrina.Core.Results;
using Doctrina.Modules.Academic.Contracts;
using Hangfire;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Doctrina.Modules.Interop.OOAPI.Client;

public static class OoapiSyncErrors
{
    public const string SourceNotFound = "Ooapi.Source.NotFound";
    public const string AlreadyRunning = "Ooapi.Sync.AlreadyRunning";
}

/// <summary>
/// Which sources are being read right now (design D8 of add-ooapi-consumer): at most one run per source. The lock is in memory;
/// the job server runs in the same process as the host. ponytail: in-process lock, a shared one if Doctrina runs on several instances.
/// </summary>
internal sealed class SyncGate
{
    private readonly ConcurrentDictionary<string, byte> _busy = new(StringComparer.OrdinalIgnoreCase);

    public bool TryClaim(string source) => _busy.TryAdd(source, 0);

    public void Release(string source) => _busy.TryRemove(source, out _);
}

/// <summary>
/// Reads an OOAPI source and imports what it finds through <see cref="IAcademicRecordImport"/> (design D2, D9 of add-ooapi-consumer).
/// <see cref="RunScheduledAsync"/> is the recurring job; <see cref="StartAsync"/> queues a run for the endpoint.
/// </summary>
internal sealed partial class OoapiSync(
    IOptions<OoapiOptions> options, IHttpClientFactory http, IAcademicRecordImport import, IBackgroundJobClient jobs, SyncGate gate,
    TimeProvider time, ILogger<OoapiSync> logger)
{
    public const string Scheduler = "schedule";

    /// <summary>The source's settings, or null if there is no such source or it is switched off.</summary>
    public OoapiSourceOptions? Find(string name) =>
        options.Value.Sources.TryGetValue(name, out var source) && source.Enabled ? source : null;

    /// <summary>Creates the job and queues the run; the job id is the answer. A source that is already being read is a conflict.</summary>
    public async Task<Result<Guid>> StartAsync(string name, bool dryRun, string startedBy, CancellationToken cancellationToken)
    {
        if (Find(name) is null)
        {
            return Error.NotFound(OoapiSyncErrors.SourceNotFound, $"There is no enabled OOAPI source '{name}'.");
        }

        if (!gate.TryClaim(name))
        {
            return Error.Conflict(OoapiSyncErrors.AlreadyRunning, $"Source '{name}' is already being read.");
        }

        try
        {
            var begun = await import.BeginAsync(new RecordImportRequest(name, $"ooapi:{name}", startedBy, dryRun), cancellationToken);
            if (begun.IsSuccess)
            {
                var jobId = begun.Value;
                jobs.Enqueue<OoapiSync>(s => s.RunQueuedAsync(name, jobId, CancellationToken.None));
                return jobId;
            }

            gate.Release(name);
            return begun;
        }
        catch
        {
            gate.Release(name);
            throw;
        }
    }

    /// <summary>The Hangfire job behind <see cref="StartAsync"/>; releases the lock <see cref="StartAsync"/> took.</summary>
    [AutomaticRetry(Attempts = 0)]
    public async Task RunQueuedAsync(string name, Guid jobId, CancellationToken cancellationToken)
    {
        try
        {
            await RunAsync(name, jobId, cancellationToken);
        }
        finally
        {
            gate.Release(name);
        }
    }

    /// <summary>The recurring job of a source with a schedule; a source that is already being read is skipped.</summary>
    [AutomaticRetry(Attempts = 0)]
    public async Task RunScheduledAsync(string name, CancellationToken cancellationToken)
    {
        if (Find(name) is null)
        {
            return;
        }

        if (!gate.TryClaim(name))
        {
            LogSkipped(logger, name);
            return;
        }

        try
        {
            var begun = await import.BeginAsync(new RecordImportRequest(name, $"ooapi:{name}", Scheduler, DryRun: false), cancellationToken);
            if (begun.IsSuccess)
            {
                await RunAsync(name, begun.Value, cancellationToken);
            }
        }
        finally
        {
            gate.Release(name);
        }
    }

    /// <summary>Reads the source and runs the job; begin and run in one go, for callers that do not need the job id first.</summary>
    internal async Task<Result<ImportRunSummary>> SyncAsync(string name, bool dryRun, string startedBy, CancellationToken cancellationToken)
    {
        var begun = await import.BeginAsync(new RecordImportRequest(name, $"ooapi:{name}", startedBy, dryRun), cancellationToken);
        return begun.IsFailure ? begun.Error! : await RunAsync(name, begun.Value, cancellationToken);
    }

    private async Task<Result<ImportRunSummary>> RunAsync(string name, Guid jobId, CancellationToken cancellationToken)
    {
        var source = Find(name);
        if (source is null)
        {
            return Error.NotFound(OoapiSyncErrors.SourceNotFound, $"There is no enabled OOAPI source '{name}'.");
        }

        var warnings = new List<string>();
        var reader = new OoapiSourceReader(new OoapiClient(http.CreateClient(OoapiClient.HttpClientName), source, time), options.Value, warnings);
        var result = await import.RunAsync(jobId, reader.ReadAsync(cancellationToken), warnings, cancellationToken);
        if (result.IsSuccess)
        {
            var s = result.Value!;
            LogRun(logger, name, jobId, s.Succeeded, s.Created, s.Updated, s.Unchanged, s.Failed);
        }

        return result;
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "OOAPI source {Source}: job {JobId} succeeded={Succeeded}, created {Created}, updated {Updated}, unchanged {Unchanged}, failed {Failed}.")]
    private static partial void LogRun(ILogger logger, string source, Guid jobId, bool succeeded, int created, int updated, int unchanged, int failed);

    [LoggerMessage(Level = LogLevel.Warning, Message = "OOAPI source {Source} is already being read; this scheduled run is skipped.")]
    private static partial void LogSkipped(ILogger logger, string source);
}
