using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace SmartDocumentProcessingSystem.DatabaseContext;

public class SDPSContextFactory : IDesignTimeDbContextFactory<SDPSContext>
{
    public SDPSContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<SDPSContext>();
        var connectionString = Environment.GetEnvironmentVariable("SDPS_CONNECTION_STRING")
            ?? "Host=localhost;Port=5432;Database=sdps;Username=postgres;Password=1234";

        optionsBuilder.UseNpgsql(connectionString);

        return new SDPSContext(optionsBuilder.Options);
    }
}
