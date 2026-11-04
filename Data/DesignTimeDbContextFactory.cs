using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
namespace ORBIT.API.Data;

// EF design-time commands must never read a production connection string.
// Migration generation requires a model, not an open database connection.
public sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args) => new(
        new DbContextOptionsBuilder<AppDbContext>().UseSqlServer(
            "Server=127.0.0.1,1;Database=ORBIT_DesignTimeOnly;User Id=unused;Password=unused;Connect Timeout=1;TrustServerCertificate=True").Options);
}
