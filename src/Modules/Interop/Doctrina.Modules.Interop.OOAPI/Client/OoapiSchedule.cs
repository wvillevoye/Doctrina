using Doctrina.Infrastructure.Jobs;
using Hangfire;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Doctrina.Modules.Interop.OOAPI.Client;

/// <summary>
/// Registers a recurring job for every enabled source with a <c>Schedule</c> when the host starts, and removes the job of a
/// source that no longer qualifies (design D9 of add-ooapi-consumer). Nothing happens when background jobs are switched off.
/// </summary>
internal sealed class OoapiSchedule(IServiceProvider services, IConfiguration configuration, IOptions<OoapiOptions> options) : IHostedService
{
    public static string JobId(string source) => $"ooapi-sync:{source}";

    public Task StartAsync(CancellationToken cancellationToken)
    {
        if (!JobServiceCollectionExtensions.IsEnabled(configuration))
        {
            return Task.CompletedTask;
        }

        var jobs = (IRecurringJobManager)(services.GetService(typeof(IRecurringJobManager))
            ?? throw new InvalidOperationException("Background jobs are enabled but IRecurringJobManager is not registered."));
        foreach (var (name, source) in options.Value.Sources)
        {
            if (source.Enabled && !string.IsNullOrWhiteSpace(source.Schedule))
            {
                var captured = name;
                jobs.AddOrUpdate<OoapiSync>(JobId(name), s => s.RunScheduledAsync(captured, CancellationToken.None), source.Schedule);
            }
            else
            {
                jobs.RemoveIfExists(JobId(name));
            }
        }

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
