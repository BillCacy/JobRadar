using JobRadar.Matching.Consumers;
using JobRadar.Matching.Data;
using MassTransit;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<MatchingDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("MatchingDb")
        ?? throw new InvalidOperationException("Missing ConnectionStrings:MatchingDb")));

builder.Services.AddMassTransit(x =>
{
    x.AddConsumer<JobsFetchedConsumer>();

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
});

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<MatchingDbContext>();
    await db.Database.EnsureCreatedAsync();
}

app.MapGet("/health", () => Results.Ok(new { status = "healthy", service = "matching" }));

app.Run();
