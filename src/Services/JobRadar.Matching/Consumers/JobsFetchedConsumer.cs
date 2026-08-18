using JobRadar.Contracts.Events;
using JobRadar.Matching.Data;
using JobRadar.Matching.Models;
using MassTransit;
using Microsoft.EntityFrameworkCore;

namespace JobRadar.Matching.Consumers;

public class JobsFetchedConsumer(MatchingDbContext db, ISendEndpointProvider sendEndpointProvider, ILogger<JobsFetchedConsumer> logger)
    : IConsumer<JobsFetched>
{
    public async Task Consume(ConsumeContext<JobsFetched> context)
    {
        var (criteria, jobs, _) = context.Message;
        var ct = context.CancellationToken;
        var newMatches = 0;

        foreach (var job in jobs)
        {
            if (!MatchingEngine.IsMatch(criteria, job))
                continue;

            var alreadySeen = await db.SeenJobs.AnyAsync(
                s => s.UserId == criteria.UserId && s.JobDedupeKey == job.DedupeKey, ct);
            if (alreadySeen)
                continue;

            db.SeenJobs.Add(new SeenJobEntity
            {
                UserId = criteria.UserId,
                CriteriaId = criteria.Id,
                JobDedupeKey = job.DedupeKey
            });

            await sendEndpointProvider.Send(new JobMatched(criteria.UserId, criteria.Id, job, DateTimeOffset.UtcNow), ct);
            newMatches++;
        }

        if (newMatches > 0)
        {
            await db.SaveChangesAsync(ct);
            logger.LogInformation("Criteria {CriteriaId}: {NewMatches} new match(es) out of {Total} fetched",
                criteria.Id, newMatches, jobs.Count);
        }
    }
}
