// PURPOSE: the REAL ag-kit EF mapping for the Office table (copied from
// ag-kit's OfficeConfiguration.cs) - table "Office", surrogate key "OKey".
using EntityManager.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Ag.Abstractions.Extensions;

namespace EntityManager.Infrastructure.Persistence.Configurations;

public class OfficeConfiguration : IEntityTypeConfiguration<Office>
{
    public void Configure(Microsoft.EntityFrameworkCore.Metadata.Builders.EntityTypeBuilder<Office> builder)
    {
        builder.ToTable("Office");
        builder.ConfigureBaseEntity();
        builder.Property(x => x.Key).HasColumnName("OKey");
        builder.Property(x => x.IsDeleted).HasColumnName("DELETED");
        builder.Property(x => x.CreatedOn).HasColumnName("CREATED");
        builder.Ignore(x => x.CreatedBy);
        builder.Ignore(x => x.LastModifiedBy);
        builder.Property(x => x.LastModifiedOn).HasColumnName("LASTMODIFIED");
    }
}
