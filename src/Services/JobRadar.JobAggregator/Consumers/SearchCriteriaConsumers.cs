using JobRadar.Contracts.Events;
using JobRadar.JobAggregator.Data;
using JobRadar.JobAggregator.Models;
using MassTransit;
using Microsoft.EntityFrameworkCore;

namespace JobRadar.JobAggregator.Consumers;

public class SearchCriteriaSavedConsumer(AggregatorDbContext db, ILogger<SearchCriteriaSavedConsumer> logger)
    : IConsumer<SearchCriteriaSaved>
{
    public async Task Consume(ConsumeContext<SearchCriteriaSaved> context)
    {
        var criteria = context.Message.Criteria;

        var watch = await db.ActiveWatches.FindAsync([criteria.Id], context.CancellationToken)
                    ?? new ActiveWatchEntity { Id = criteria.Id };

        watch.UserId = criteria.UserId;
        watch.Keywords = criteria.Keywords;
        watch.Location = criteria.Location;
        watch.MinSalary = criteria.MinSalary;
        watch.RemoteOnly = criteria.RemoteOnly;
        watch.JobType = criteria.JobType;
        watch.ExcludeKeywordsCsv = string.Join(',', criteria.ExcludeKeywords);
        watch.IsActive = true;

        if (db.Entry(watch).State == EntityState.Detached)
            db.ActiveWatches.Add(watch);

        await db.SaveChangesAsync(context.CancellationToken);
        logger.LogInformation("Upserted active watch {WatchId} for user {UserId} ('{Keywords}' in '{Location}')",
            watch.Id, watch.UserId, watch.Keywords, watch.Location);
    }
}

public class SearchCriteriaDeletedConsumer(AggregatorDbContext db, ILogger<SearchCriteriaDeletedConsumer> logger)
    : IConsumer<SearchCriteriaDeleted>
{
    public async Task Consume(ConsumeContext<SearchCriteriaDeleted> context)
    {
        var watch = await db.ActiveWatches.FindAsync([context.Message.CriteriaId], context.CancellationToken);
        if (watch is null)
            return;

        watch.IsActive = false;
        await db.SaveChangesAsync(context.CancellationToken);
        logger.LogInformation("Deactivated watch {WatchId}", watch.Id);
    }
}
