// PURPOSE: the REAL ag-kit EF mapping for the Company table (copied from
// ag-kit's CompanyConfiguration.cs) - table "Company", surrogate key "CKey".
using EntityManager.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Ag.Abstractions.Extensions;

namespace EntityManager.Infrastructure.Persistence.Configurations;

public class CompanyConfiguration : IEntityTypeConfiguration<Company>
{
    public void Configure(Microsoft.EntityFrameworkCore.Metadata.Builders.EntityTypeBuilder<Company> builder)
    {
        builder.ToTable("Company");
        builder.ConfigureBaseEntity();
        builder.Property(x => x.Key).HasColumnName("CKey");
        builder.Property(x => x.IsDeleted).HasColumnName("DELETED");
        builder.Property(x => x.CreatedOn).HasColumnName("CREATED");
        builder.Ignore(x => x.CreatedBy);
        builder.Ignore(x => x.LastModifiedBy);
        builder.Property(x => x.LastModifiedOn).HasColumnName("LASTMODIFIED");
    }
}
