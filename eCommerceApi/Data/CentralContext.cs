using Microsoft.EntityFrameworkCore;
using eCommerceApi.Models;

namespace eCommerceApi.Data
{
    public class CentralContext : DbContext
    {
        public CentralContext(DbContextOptions<CentralContext> options) : base(options)
        {
        }

        public DbSet<Store> Stores { get; set; }
        public DbSet<AdminConfig> AdminConfig { get; set; }
    }
}