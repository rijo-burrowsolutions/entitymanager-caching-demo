// PURPOSE: the EF Core DbContext, now pointed at real SQL Server instead of
// SQLite. Structurally identical to the real ag-kit EntityManagerDBContext -
// same ApplyConfigurationsFromAssembly convention-scan, just matching this
// project's "Persistence.Configurations" namespace instead of ag-kit's
// (misspelled) "Persistance.Configurations".
using Microsoft.EntityFrameworkCore;

namespace EntityManager.Infrastructure.Persistence;

public class EntityManagerDbContext : DbContext
{
    public EntityManagerDbContext(DbContextOptions<EntityManagerDbContext> options) : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(EntityManagerDbContext).Assembly, t => t.Namespace != null && t.Namespace.Contains("Persistence.Configurations"));
    }
}
