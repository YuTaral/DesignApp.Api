using DesignApp.Domain.Designs;
using DesignApp.Domain.Rooms;
using DesignApp.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace DesignApp.Infrastructure.Persistence;

/// <summary>The EF Core database session for Postgres: the app's tables and ASP.NET Core Identity's accounts.</summary>
/// <param name="options">Connection and provider settings.</param>
public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : IdentityDbContext<AppUser>(options)
{
    /// <summary>The rooms table.</summary>
    public DbSet<Room> Rooms => Set<Room>();

    /// <summary>The saved designs table.</summary>
    public DbSet<Design> Designs => Set<Design>();

    /// <summary>
    /// Adds Identity's tables, then applies every entity configuration in this assembly, such as
    /// <see cref="RoomConfiguration"/>.
    /// </summary>
    /// <param name="builder">Builds the database model.</param>
    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        // Identity names its tables like "AspNetUsers"; keep them snake_case like the rest.
        builder.Entity<AppUser>().ToTable("users");
        builder.Entity<IdentityRole>().ToTable("roles");
        builder.Entity<IdentityUserClaim<string>>().ToTable("user_claims");
        builder.Entity<IdentityUserLogin<string>>().ToTable("user_logins");
        builder.Entity<IdentityUserRole<string>>().ToTable("user_roles");
        builder.Entity<IdentityUserToken<string>>().ToTable("user_tokens");
        builder.Entity<IdentityRoleClaim<string>>().ToTable("role_claims");
        builder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }
}
