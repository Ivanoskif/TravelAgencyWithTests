using System.IO;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;
namespace TravelAgency.Repository.Data
{
    public class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
    {
        public AppDbContext CreateDbContext(string[] args)
        {
            var env = System.Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Development";

            var config = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile(Path.Combine("..", "TravelAgency.Web", "appsettings.json"), optional: true)
                .AddJsonFile(Path.Combine("..", "TravelAgency.Web", $"appsettings.{env}.json"), optional: true)
                .AddEnvironmentVariables()
                .Build();

            var conn = config.GetConnectionString("Default") ?? "Data Source=travel_agency.db";

            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlite(conn)
                .Options;

            return new AppDbContext(options);
        }
    }
}
