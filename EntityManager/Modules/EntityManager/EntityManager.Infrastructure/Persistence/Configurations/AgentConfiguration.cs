// PURPOSE: the REAL ag-kit EF mapping for the Agent table - copied from
// ag-kit/Modules/EntityManager/EntityManager.Infrastructure/Persistance/Configurations/AgentConfiguration.cs.
// idc_ety's real table is named "Agent" (singular) with a surrogate key
// column "AKey" - nothing like the demo's earlier simplified "Agents"/"AKey"
// guesswork; this is copied straight from what already works in production.
using EntityManager.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Ag.Abstractions.Extensions;

namespace EntityManager.Infrastructure.Persistence.Configurations;

public class AgentConfiguration : IEntityTypeConfiguration<Agent>
{
    public void Configure(Microsoft.EntityFrameworkCore.Metadata.Builders.EntityTypeBuilder<Agent> builder)
    {
        builder.ToTable("Agent");
        builder.ConfigureBaseEntity();
        builder.Property(x => x.Key).HasColumnName("AKey");
        builder.Property(x => x.IsDeleted).HasColumnName("DELETED");
        builder.Property(x => x.CreatedOn).HasColumnName("CREATED");
        builder.Ignore(x => x.CreatedBy);
        builder.Ignore(x => x.LastModifiedBy);
        builder.Property(x => x.LastModifiedOn).HasColumnName("LASTMODIFIED");
        builder.Property(x => x.IsDisplayedOnWebsite).HasColumnName("DISPLAYONWEBSITE");
    }
}
