using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace DesignApp.Infrastructure.Database;

/// <summary>Lets <c>dotnet ef migrations add</c> build the model without starting the API or a database.</summary>
internal sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    /// <summary>Creates a context with a placeholder connection string; EF tools never connect with it.</summary>
    /// <param name="args">Arguments from the EF tools (unused).</param>
    public AppDbContext CreateDbContext(string[] args) =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(Constants.Config.DesignTimeConnection)
            .UseSnakeCaseNamingConvention()
            .Options);
}
