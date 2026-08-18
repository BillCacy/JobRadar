using JobRadar.Matching.Consumers;
using JobRadar.Matching.Data;
using MassTransit;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<MatchingDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("MatchingDb")
        ?? throw new InvalidOperationException("Missing ConnectionStrings:MatchingDb")));

var messagingTransport = builder.Configuration["Messaging:Transport"] ?? "RabbitMq";

builder.Services.AddMassTransit(x =>
{
    x.AddConsumer<JobsFetchedConsumer>();

    if (string.Equals(messagingTransport, "AzureServiceBus", StringComparison.OrdinalIgnoreCase))
    {
        x.UsingAzureServiceBus((context, cfg) =>
        {
            cfg.Host(builder.Configuration["AzureServiceBus:ConnectionString"]
                ?? throw new InvalidOperationException("Missing AzureServiceBus:ConnectionString"));

            cfg.ReceiveEndpoint("matching-jobsfetched", e =>
            {
                e.ConfigureConsumer<JobsFetchedConsumer>(context);
            });
        });
    }
    else
    {
        x.UsingRabbitMq((context, cfg) =>
        {
            cfg.Host(builder.Configuration["RabbitMq:Host"] ?? "localhost", "/", h =>
            {
                h.Username(builder.Configuration["RabbitMq:User"] ?? "guest");
                h.Password(builder.Configuration["RabbitMq:Password"] ?? "guest");
            });

            cfg.ReceiveEndpoint("matching-jobsfetched", e =>
            {
                e.ConfigureConsumer<JobsFetchedConsumer>(context);
            });
        });
    }
});

// Matching publishes JobMatched directly to Notifications' queue rather than through a
// topic/exchange fan-out - there's exactly one consumer, and point-to-point Send lets this run
// on Azure Service Bus's Basic tier (queues only, no topics) instead of the pricier Standard
// tier. Same mapping works unchanged against the RabbitMQ transport too.
EndpointConvention.Map<JobRadar.Contracts.Events.JobMatched>(new Uri("queue:notifications-jobmatched"));

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<MatchingDbContext>();
    await db.Database.EnsureCreatedAsync();
}

app.MapGet("/health", () => Results.Ok(new { status = "healthy", service = "matching" }));

app.Run();
