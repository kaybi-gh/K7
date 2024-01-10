using System.Reflection;
using MediaServer.Application.Common.Interfaces;
using MediaServer.Domain.Entities;
using MediaServer.Domain.Entities.Medias;
using MediaServer.Infrastructure.Context.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace MediaServer.Infrastructure.Context.Data;

public class ApplicationDbContext : IdentityDbContext<ApplicationUser>, IApplicationDbContext
{
    public ApplicationDbContext() { }
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options) { }

    public DbSet<IndexedFile> IndexedFiles => Set<IndexedFile>();
    public DbSet<Library> Libraries => Set<Library>();
    public DbSet<BaseMedia> Medias => Set<BaseMedia>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());
    }
}
