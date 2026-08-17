using JobRadar.JobAggregator.Connectors;
using JobRadar.JobAggregator.Consumers;
using JobRadar.JobAggregator.Data;
using JobRadar.JobAggregator.Jobs;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Quartz;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<AggregatorDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("AggregatorDb")
        ?? throw new InvalidOperationException("Missing ConnectionStrings:AggregatorDb")));

// --- Job connectors: add a new one here + register it below to plug in another job site ---
builder.Services.Configure<AdzunaOptions>(builder.Configuration.GetSection("Adzuna"));
builder.Services.Configure<JoobleOptions>(builder.Configuration.GetSection("Jooble"));

builder.Services.AddHttpClient<AdzunaConnector>(c => c.BaseAddress = new Uri("https://api.adzuna.com/"));
builder.Services.AddHttpClient<JoobleConnector>(c => c.BaseAddress = new Uri("https://jooble.org/"));
builder.Services.AddScoped<IJobConnector, AdzunaConnector>();
builder.Services.AddScoped<IJobConnector, JoobleConnector>();

builder.Services.AddMassTransit(x =>
{
    x.AddConsumer<SearchCriteriaSavedConsumer>();
    x.AddConsumer<SearchCriteriaDeletedConsumer>();

    x.UsingRabbitMq((context, cfg) =>
    {
        cfg.Host(builder.Configuration["RabbitMq:Host"] ?? "localhost", "/", h =>
        {
            h.Username(builder.Configuration["RabbitMq:User"] ?? "guest");
            h.Password(builder.Configuration["RabbitMq:Password"] ?? "guest");
        });

        // Durable queue named after this service so restarts don't lose in-flight events.
        cfg.ReceiveEndpoint("jobaggregator-criteria-events", e =>
        {
            e.ConfigureConsumer<SearchCriteriaSavedConsumer>(context);
            e.ConfigureConsumer<SearchCriteriaDeletedConsumer>(context);
        });
    });
});

var pollIntervalMinutes = builder.Configuration.GetValue("Aggregator:PollIntervalMinutes", 10);

builder.Services.AddQuartz(q =>
{
    var jobKey = new JobKey(nameof(PollActiveWatchesJob));
    q.AddJob<PollActiveWatchesJob>(opts => opts.WithIdentity(jobKey));
    q.AddTrigger(opts => opts
        .ForJob(jobKey)
        .WithIdentity($"{nameof(PollActiveWatchesJob)}-trigger")
        .StartAt(DateTimeOffset.UtcNow.AddSeconds(15)) // brief delay so RabbitMQ/Postgres are up
        .WithSimpleSchedule(s => s.WithIntervalInMinutes(pollIntervalMinutes).RepeatForever()));
});
builder.Services.AddQuartzHostedService(opts => opts.WaitForJobsToComplete = true);

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AggregatorDbContext>();
    await db.Database.EnsureCreatedAsync();
}

app.MapGet("/health", () => Results.Ok(new { status = "healthy", service = "jobaggregator" }));

app.Run();
