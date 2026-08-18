using JobRadar.Contracts.Events;
using JobRadar.Users.Data;
using MassTransit;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(p => p.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod());
});

builder.Services.AddDbContext<UsersDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("UsersDb")
        ?? throw new InvalidOperationException("Missing ConnectionStrings:UsersDb")));

var messagingTransport = builder.Configuration["Messaging:Transport"] ?? "RabbitMq";

builder.Services.AddMassTransit(x =>
{
    if (string.Equals(messagingTransport, "AzureServiceBus", StringComparison.OrdinalIgnoreCase))
    {
        x.UsingAzureServiceBus((context, cfg) =>
        {
            cfg.Host(builder.Configuration["AzureServiceBus:ConnectionString"]
                ?? throw new InvalidOperationException("Missing AzureServiceBus:ConnectionString"));
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
        });
    }
});

// Users publishes both criteria events directly to JobAggregator's queue - see the matching
// service's Program.cs for why this is Send-to-queue instead of Publish-to-topic.
EndpointConvention.Map<SearchCriteriaSaved>(new Uri("queue:jobaggregator-criteria-events"));
EndpointConvention.Map<SearchCriteriaDeleted>(new Uri("queue:jobaggregator-criteria-events"));

var app = builder.Build();

// EnsureCreated (not migrations) keeps "clone and run" simple for a portfolio project -
// a real production service would use `dotnet ef migrations` instead.
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<UsersDbContext>();
    await db.Database.EnsureCreatedAsync();
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors();
app.MapControllers();
app.MapGet("/health", () => Results.Ok(new { status = "healthy", service = "users" }));

app.Run();
