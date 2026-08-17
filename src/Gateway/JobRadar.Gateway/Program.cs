var builder = WebApplication.CreateBuilder(args);

builder.Services.AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));

// Same reasoning as JobRadar.Notifications: SignalR needs credentialed CORS, which means we
// can't use AllowAnyOrigin. Fine for local/dev; tighten the origin allow-list before deploying
// anywhere public.
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(p => p
        .SetIsOriginAllowed(_ => true)
        .AllowAnyHeader()
        .AllowAnyMethod()
        .AllowCredentials());
});

var app = builder.Build();

// Required so the SignalR hub's WebSocket upgrade (proxied through to notifications-service)
// survives the trip through YARP instead of getting treated as a dead HTTP request.
app.UseWebSockets();
app.UseCors();

app.MapGet("/health", () => Results.Ok(new { status = "healthy", service = "gateway" }));
app.MapReverseProxy();

app.Run();
