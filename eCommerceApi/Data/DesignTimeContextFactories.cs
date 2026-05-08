using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;
using System.Reflection;

namespace eCommerceApi.Data
{
    public class CentralContextFactory : IDesignTimeDbContextFactory<CentralContext>
    {
        public CentralContext CreateDbContext(string[] args)
        {
            var cs = BuildConnectionString();
            var options = new DbContextOptionsBuilder<CentralContext>()
                .UseMySql(cs, new MySqlServerVersion(new Version(5, 7)))
                .Options;
            return new CentralContext(options);
        }

        private static string BuildConnectionString()
        {
            return new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json")
                .AddJsonFile("appsettings.Development.json", optional: true)
                .AddUserSecrets(Assembly.GetExecutingAssembly(), optional: true)
                .AddEnvironmentVariables()
                .Build()
                .GetConnectionString("DefaultConnection")!;
        }
    }

    public class EcommerceContextFactory : IDesignTimeDbContextFactory<EcommerceContext>
    {
        public EcommerceContext CreateDbContext(string[] args)
        {
            var cs = BuildConnectionString();
            var options = new DbContextOptionsBuilder<EcommerceContext>()
                .UseMySql(cs, new MySqlServerVersion(new Version(5, 7)))
                .Options;
            return new EcommerceContext(options);
        }

        private static string BuildConnectionString()
        {
            return new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json")
                .AddJsonFile("appsettings.Development.json", optional: true)
                .AddUserSecrets(Assembly.GetExecutingAssembly(), optional: true)
                .AddEnvironmentVariables()
                .Build()
                .GetConnectionString("DefaultConnection")!;
        }
    }
}
