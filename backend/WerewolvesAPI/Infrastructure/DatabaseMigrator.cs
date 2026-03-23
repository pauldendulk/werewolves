using DbUp;

namespace WerewolvesAPI.Infrastructure;

public static class DatabaseMigrator
{
    public static void Run(string connectionString, ILogger logger)
    {
        EnsureDatabase.For.PostgresqlDatabase(connectionString);

        var upgrader = DeployChanges.To
            .PostgresqlDatabase(connectionString)
            .WithScriptsEmbeddedInAssembly(typeof(DatabaseMigrator).Assembly)
            .WithTransaction()
            .LogTo(new DbUpLogger(logger))
            .Build();

        var result = upgrader.PerformUpgrade();

        if (!result.Successful)
            throw new Exception("Database migration failed", result.Error);
    }

    private sealed class DbUpLogger(ILogger logger) : DbUp.Engine.Output.IUpgradeLog
    {
        public void WriteInformation(string format, params object[] args) =>
            logger.LogInformation(format, args);

        public void WriteWarning(string format, params object[] args) =>
            logger.LogWarning(format, args);

        public void WriteError(string format, params object[] args) =>
            logger.LogError(format, args);
    }
}
