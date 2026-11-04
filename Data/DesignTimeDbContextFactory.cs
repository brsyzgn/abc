using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
namespace ORBIT.API.Data;

// EF design-time commands must never read a production connection string.
// Migration generation requires a model, not an open database connection.
public sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args) => new(
        new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(
            "Host=127.0.0.1;Port=1;Database=orbit_designtime_only;Username=unused;Password=unused;Timeout=1").Options);
}
