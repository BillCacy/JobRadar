using JobRadar.Contracts.Events;
using JobRadar.JobAggregator.Connectors;
using JobRadar.JobAggregator.Data;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Quartz;

namespace JobRadar.JobAggregator.Jobs;

/// <summary>
/// The heartbeat of the whole app. On a fixed interval (Aggregator:PollIntervalMinutes),
/// re-runs every active watch's query against every configured IJobConnector and publishes
/// one JobsFetched event per watch. Matching decides what to do with the results — this job
/// doesn't know or care what a "match" means.
/// </summary>
public class PollActiveWatchesJob(
    AggregatorDbContext db,
    IEnumerable<IJobConnector> connectors,
    IPublishEndpoint publishEndpoint,
    ILogger<PollActiveWatchesJob> logger) : IJob
{
    public async Task Execute(IJobExecutionContext context)
    {
        var ct = context.CancellationToken;
        var activeConnectors = connectors.Where(c => c.IsConfigured).ToList();

        if (activeConnectors.Count == 0)
        {
            logger.LogWarning("No job connectors are configured (missing API keys) — skipping poll cycle.");
            return;
        }

        var watches = await db.ActiveWatches.Where(w => w.IsActive).ToListAsync(ct);
        logger.LogInformation("Polling {WatchCount} active watch(es) across connectors: {Connectors}",
            watches.Count, string.Join(", ", activeConnectors.Select(c => c.Name)));

        foreach (var watch in watches)
        {
            var query = new JobSearchQuery(watch.Keywords, watch.Location);
            var jobs = new List<Contracts.Models.JobPosting>();

            foreach (var connector in activeConnectors)
            {
                try
                {
                    var results = await connector.SearchAsync(query, ct);
                    jobs.AddRange(results);
                }
                catch (Exception ex)
                {
                    // One connector having a bad day (rate limit, outage) shouldn't stop the
                    // others, or stop other watches from being polled.
                    logger.LogError(ex, "Connector {Connector} failed for watch {WatchId}", connector.Name, watch.Id);
                }
            }

            watch.LastPolledAt = DateTimeOffset.UtcNow;

            if (jobs.Count > 0)
            {
                await publishEndpoint.Publish(new JobsFetched(watch.ToContract(), jobs, DateTimeOffset.UtcNow), ct);
            }
        }

        await db.SaveChangesAsync(ct);
    }
}
