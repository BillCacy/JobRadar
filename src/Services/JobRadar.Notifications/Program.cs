using JobRadar.Notifications.Consumers;
using JobRadar.Notifications.Data;
using JobRadar.Notifications.Hubs;
using MassTransit;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddSignalR();

// AllowCredentials + AllowAnyOrigin can't be combined, and SignalR needs credentials, so we
// echo back whatever origin asked (fine for a local/dev "clone and run" demo, tighten before
// this ever sees a public network).
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(p => p
        .SetIsOriginAllowed(_ => true)
        .AllowAnyHeader()
        .AllowAnyMethod()
        .AllowCredentials());
});

builder.Services.AddDbContext<NotificationsDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("NotificationsDb")
        ?? throw new InvalidOperationException("Missing ConnectionStrings:NotificationsDb")));

var messagingTransport = builder.Configuration["Messaging:Transport"] ?? "RabbitMq";

builder.Services.AddMassTransit(x =>
{
    x.AddConsumer<JobMatchedConsumer>();

    if (string.Equals(messagingTransport, "AzureServiceBus", StringComparison.OrdinalIgnoreCase))
    {
        x.UsingAzureServiceBus((context, cfg) =>
        {
            cfg.Host(builder.Configuration["AzureServiceBus:ConnectionString"]
                ?? throw new InvalidOperationException("Missing AzureServiceBus:ConnectionString"));

            cfg.ReceiveEndpoint("notifications-jobmatched", e =>
            {
                e.ConfigureConsumer<JobMatchedConsumer>(context);
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

            cfg.ReceiveEndpoint("notifications-jobmatched", e =>
            {
                e.ConfigureConsumer<JobMatchedConsumer>(context);
            });
        });
    }
});

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<NotificationsDbContext>();
    await db.Database.EnsureCreatedAsync();
}

app.UseCors();
app.MapControllers();
app.MapHub<JobAlertsHub>("/hubs/job-alerts");
app.MapGet("/health", () => Results.Ok(new { status = "healthy", service = "notifications" }));

app.Run();
