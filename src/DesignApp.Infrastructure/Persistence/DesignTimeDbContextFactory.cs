using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace DesignApp.Infrastructure.Persistence;

/// <summary>Lets <c>dotnet ef migrations add</c> build the model without starting the API or a database.</summary>
internal sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args) =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql("Host=localhost;Database=designapp")
            .UseSnakeCaseNamingConvention()
            .Options);
}
