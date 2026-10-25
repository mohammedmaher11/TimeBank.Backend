using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TimeBank.Infrastructure.Persistence;

namespace TimeBank.IntegrationTests.Infrastructure;

/// <summary>
/// Starts the real API against a unique SQL Server database, applies the real EF Core
/// migrations and runs the real DataSeeder. The database is dropped when the fixture is
/// disposed. No InMemory provider is used anywhere.
/// </summary>
public sealed class TimeBankApiFactory : WebApplicationFactory<Program>
{
    public const string TestJwtKey =
        "INTEGRATION-TEST-ONLY-signing-key-with-more-than-32-bytes-2026";

    public string DatabaseName { get; } = $"TimeBankDb_Test_{Guid.NewGuid():N}";

    private string ConnectionString =>
        $"Server=localhost\\SQLEXPRESS;Database={DatabaseName};Trusted_Connection=True;TrustServerCertificate=True";

    public HttpClient Client { get; }

    public TimeBankApiFactory()
    {
        Client = CreateClient();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");

        // UseSetting feeds the host configuration, which is available while Program.cs
        // reads JwtOptions during startup. ConfigureAppConfiguration alone can be applied
        // after the app's configuration has already been read in minimal hosting.
        builder.UseSetting("ConnectionStrings:DefaultConnection", ConnectionString);
        builder.UseSetting("Jwt:Issuer", "TimeBank.API.IntegrationTests");
        builder.UseSetting("Jwt:Audience", "TimeBank.Client.IntegrationTests");
        builder.UseSetting("Jwt:Key", TestJwtKey);
        builder.UseSetting("Jwt:ExpiryMinutes", "120");
        builder.UseSetting("Database:ApplyMigrationsOnStartup", "true");
        builder.UseSetting("Database:SeedOnStartup", "true");
        builder.UseSetting("Logging:LogLevel:Default", "Warning");
        builder.UseSetting("Logging:LogLevel:Microsoft.AspNetCore", "Warning");
        builder.UseSetting("Logging:LogLevel:Microsoft.EntityFrameworkCore", "Warning");

        builder.ConfigureAppConfiguration((_, configuration) =>
        {
            configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = ConnectionString,
                ["Jwt:Issuer"] = "TimeBank.API.IntegrationTests",
                ["Jwt:Audience"] = "TimeBank.Client.IntegrationTests",
                ["Jwt:Key"] = TestJwtKey,
                ["Jwt:ExpiryMinutes"] = "120",
                ["Database:ApplyMigrationsOnStartup"] = "true",
                ["Database:SeedOnStartup"] = "true",
                ["Logging:LogLevel:Default"] = "Warning",
                ["Logging:LogLevel:Microsoft.AspNetCore"] = "Warning",
                ["Logging:LogLevel:Microsoft.EntityFrameworkCore"] = "Warning"
            });
        });
    }

    public IServiceScope CreateScope() => Services.CreateScope();

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            DropDatabase();
        }

        base.Dispose(disposing);
    }

    private void DropDatabase()
    {
        try
        {
            SqlConnection.ClearAllPools();

            var masterConnection =
                "Server=localhost\\SQLEXPRESS;Database=master;Trusted_Connection=True;TrustServerCertificate=True";

            using var connection = new SqlConnection(masterConnection);
            connection.Open();

            using var command = connection.CreateCommand();
            command.CommandText = $"""
                IF DB_ID(N'{DatabaseName}') IS NOT NULL
                BEGIN
                    ALTER DATABASE [{DatabaseName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
                    DROP DATABASE [{DatabaseName}];
                END
                """;
            command.CommandTimeout = 60;
            command.ExecuteNonQuery();
        }
        catch
        {
            // Test cleanup must not hide the real test result.
        }
        finally
        {
            SqlConnection.ClearAllPools();
        }
    }
}
