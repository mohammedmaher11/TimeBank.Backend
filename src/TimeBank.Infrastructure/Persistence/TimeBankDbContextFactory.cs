using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace TimeBank.Infrastructure.Persistence;

/// <summary>
/// Used by `dotnet ef` at design time. The connection string is never hardcoded with a
/// password: set TIMEBANK_CONNECTION (or ConnectionStrings__DefaultConnection) if needed.
/// </summary>
public sealed class TimeBankDbContextFactory : IDesignTimeDbContextFactory<TimeBankDbContext>
{
    public TimeBankDbContext CreateDbContext(string[] args)
    {
        var connectionString =
            Environment.GetEnvironmentVariable("TIMEBANK_CONNECTION")
            ?? Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection")
            ?? @"Server=localhost\SQLEXPRESS;Database=TimeBankDb;Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=True";

        var options = new DbContextOptionsBuilder<TimeBankDbContext>()
            .UseSqlServer(connectionString, sql =>
            {
                sql.MigrationsAssembly(typeof(TimeBankDbContext).Assembly.FullName);
            })
            .Options;

        return new TimeBankDbContext(options);
    }
}
