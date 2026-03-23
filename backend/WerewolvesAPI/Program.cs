using WerewolvesAPI.Infrastructure;
using WerewolvesAPI.Repositories;
using WerewolvesAPI.Services;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") ?? string.Empty;

// Add services to the container
builder.Services.AddControllers();
builder.Services.AddSingleton<IGameRepository>(_ => new GameRepository(connectionString));
builder.Services.AddSingleton<ITournamentRepository>(_ => new TournamentRepository(connectionString));
builder.Services.AddSingleton<IGameService, GameService>();

// Add CORS
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

// Add OpenAPI/Swagger
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// Run database migrations on startup (skipped when connection string is empty)
if (!string.IsNullOrEmpty(connectionString))
{
    var migratorLogger = app.Services.GetRequiredService<ILogger<Program>>();
    DatabaseMigrator.Run(connectionString, migratorLogger);
}

// Configure the HTTP request pipeline
app.UseSwagger();
app.UseSwaggerUI();

app.UseCors("AllowAngular");

app.UseHttpsRedirection();
app.UseAuthorization();

app.MapControllers();

app.Run();
