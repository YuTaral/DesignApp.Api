using DesignApp.Domain.Designs;
using DesignApp.Domain.Rooms;
using DesignApp.Infrastructure.Ai;
using DesignApp.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace DesignApp.Infrastructure.Database;

/// <summary>The EF Core database session for Postgres: the app's tables and ASP.NET Core Identity's accounts.</summary>
/// <param name="options">Connection and provider settings.</param>
public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : IdentityDbContext<AppUser>(options)
{
    /// <summary>The rooms table.</summary>
    public DbSet<Room> Rooms => Set<Room>();

    /// <summary>The saved designs table.</summary>
    public DbSet<Design> Designs => Set<Design>();

    /// <summary>The one-row table of AI settings (see <see cref="Ai.AiSettings"/>).</summary>
    public DbSet<AiSettings> AiSettings => Set<AiSettings>();

    /// <summary>
    /// Adds Identity's tables, then applies every entity configuration in this assembly, such as
    /// <see cref="RoomConfiguration"/>.
    /// </summary>
    /// <param name="builder">Builds the database model.</param>
    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        // Identity names its tables like "AspNetUsers"; keep them snake_case like the rest.
        builder.Entity<AppUser>().ToTable(Constants.Persistence.Tables.Users);
        builder.Entity<IdentityRole>().ToTable(Constants.Persistence.Tables.Roles);
        builder.Entity<IdentityUserClaim<string>>().ToTable(Constants.Persistence.Tables.UserClaims);
        builder.Entity<IdentityUserLogin<string>>().ToTable(Constants.Persistence.Tables.UserLogins);
        builder.Entity<IdentityUserRole<string>>().ToTable(Constants.Persistence.Tables.UserRoles);
        builder.Entity<IdentityUserToken<string>>().ToTable(Constants.Persistence.Tables.UserTokens);
        builder.Entity<IdentityRoleClaim<string>>().ToTable(Constants.Persistence.Tables.RoleClaims);
        builder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }
}
