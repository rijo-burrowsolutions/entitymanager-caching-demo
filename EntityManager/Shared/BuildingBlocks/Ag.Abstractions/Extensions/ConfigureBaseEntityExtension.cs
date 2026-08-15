// PURPOSE: copied verbatim from the real ag-kit Ag.Abstractions - the EF
// Core mapping every real AgentConfiguration/OfficeConfiguration/
// CompanyConfiguration calls to wire up BaseEntity's shared columns.
namespace Ag.Abstractions.Extensions;

using Ag.Abstractions.Entities;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

public static class ConfigureBaseEntityExtension
{
    public static EntityTypeBuilder<TEntity> ConfigureBaseEntity<TEntity>(this EntityTypeBuilder<TEntity> builder) where TEntity : BaseEntity
    {
        builder.HasKey(x => x.Key);

        builder.Property(x => x.Guid)
            .IsRequired();

        builder.Property(x => x.LastModifiedBy)
            .IsRequired();

        builder.Property(x => x.LastModifiedOn)
            .IsRequired();
        builder.Property(x => x.CreatedOn)
            .IsRequired();

        builder.Property(x => x.IsDeleted)
            .IsRequired();

        builder.Property(x => x.Revision)
            .IsRequired();

        return builder;
    }
}
