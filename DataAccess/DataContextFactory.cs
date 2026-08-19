using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace DataAccess
{
    /// <summary>
    /// Design-time factory for EF Core migrations (dotnet ef).
    /// Prefers ASPNETCORE_ENVIRONMENT (default Development) so local tests
    /// use appsettings.Development.json instead of the production server DB.
    /// </summary>
    internal class DataContextFactory : IDesignTimeDbContextFactory<DataContext>
    {
        public DataContext CreateDbContext(string[] args)
        {
            // The EF Core CLI runs with the startup project (PMS.web) as the current
            // directory, but fall back to it explicitly in case this factory is ever
            // invoked from the DataAccess project directory instead.
            var basePath = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "..", "PMS.web"));
            if (!File.Exists(Path.Combine(basePath, "appsettings.json")))
                basePath = Directory.GetCurrentDirectory();

            var environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")
                ?? Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT")
                ?? "Development";

            var configuration = new ConfigurationBuilder()
                .SetBasePath(basePath)
                .AddJsonFile("appsettings.json", optional: false)
                .AddJsonFile($"appsettings.{environment}.json", optional: true)
                .AddEnvironmentVariables()
                .Build();

            var connectionString = configuration.GetConnectionString("DefaultConnection")
                ?? throw new InvalidOperationException(
                    $"Connection string 'DefaultConnection' not found (environment: {environment}).");

            Console.WriteLine($"[EF Design] Environment={environment}");
            Console.WriteLine($"[EF Design] Connection={MaskConnectionString(connectionString)}");

            var optionsBuilder = new DbContextOptionsBuilder<DataContext>();
            optionsBuilder.UseSqlServer(connectionString);

            return new DataContext(optionsBuilder.Options);
        }

        private static string MaskConnectionString(string cs)
        {
            // Hide password if present; keep server/database visible for confirmation.
            var parts = cs.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            return string.Join(';', parts.Select(p =>
                p.StartsWith("Password=", StringComparison.OrdinalIgnoreCase)
                || p.StartsWith("Pwd=", StringComparison.OrdinalIgnoreCase)
                    ? p.Split('=')[0] + "=***"
                    : p));
        }
    }
}
