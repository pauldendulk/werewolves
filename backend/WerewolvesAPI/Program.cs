using WerewolvesAPI.Infrastructure;
using WerewolvesAPI.Repositories;
using WerewolvesAPI.Services;

// Crash the process on any unobserved task exception — we prefer loud failures over silent data loss.
TaskScheduler.UnobservedTaskException += (_, args) =>
{
    args.SetObserved();
    Environment.FailFast("Unobserved task exception", args.Exception);
};

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") ?? string.Empty;

builder.Services.AddControllers();
builder.Services.AddSingleton<IGameRepository>(_ => new GameRepository(connectionString));
builder.Services.AddSingleton<ITournamentRepository>(_ => new TournamentRepository(connectionString));
builder.Services.AddSingleton<IPromoCodeRepository>(_ => new PromoCodeRepository(connectionString));
builder.Services.AddSingleton<IGameService, GameService>();
builder.Services.AddSingleton<IStripeService, StripeService>();

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAngular", policy =>
    {
        policy.WithOrigins(
                  "http://localhost:4200",
                  "http://localhost:4201",
                  "https://werewolves-app-brutiledemo.web.app",
                  "https://werewolves-app-brutiledemo.firebaseapp.com")
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// Run database migrations on startup (skipped when connection string is empty)
if (!string.IsNullOrEmpty(connectionString))
{
    var migratorLogger = app.Services.GetRequiredService<ILogger<Program>>();
    DatabaseMigrator.Run(connectionString, migratorLogger);
    await app.Services.GetRequiredService<IGameService>().InitializeAsync();
}

// Must be first so it wraps CORS, ensuring error responses also carry CORS headers.
app.UseExceptionHandler("/error");

app.UseSwagger();
app.UseSwaggerUI();

app.UseCors("AllowAngular");

app.UseAuthorization();

app.MapControllers();

app.Map("/error", (HttpContext httpContext) =>
{
    var feature = httpContext.Features.Get<Microsoft.AspNetCore.Diagnostics.IExceptionHandlerFeature>();
    var ex = feature?.Error;
    var logger = httpContext.RequestServices.GetRequiredService<ILogger<Program>>();
    logger.LogError(ex, "Unhandled exception");
    return Results.Problem(statusCode: 500, detail: "An unexpected error occurred.");
});

app.Run();
