using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Ayurveda.Infrastructure.Data;

public class AyurvedaDbContextFactory : IDesignTimeDbContextFactory<AyurvedaDbContext>
{
    public AyurvedaDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<AyurvedaDbContext>();
        optionsBuilder.UseSqlServer("Server=(localdb)\\mssqllocaldb;Database=AyurvedaDb;Trusted_Connection=True;MultipleActiveResultSets=true;TrustServerCertificate=True");

        return new AyurvedaDbContext(optionsBuilder.Options);
    }
}
