// PURPOSE: copied verbatim from the real ag-kit Ag.Abstractions - every real
// Agent/Office/Company entity inherits this for its Key/Guid/soft-delete/
// audit fields. Needed as-is so the real entity classes compile unchanged.
namespace Ag.Abstractions.Entities;

using Ag.Abstractions.Command;

public abstract class BaseEntity : Pagination
{
    public int Key { get; private set; }
    public Guid Guid { get; private set; } = Guid.NewGuid();
    public bool IsDeleted { get; set; } = false;
    public DateTime CreatedOn { get; private set; } = DateTime.UtcNow;
    public DateTime LastModifiedOn { get; private set; } = DateTime.UtcNow;
    public int CreatedBy { get; private set; } = 8;
    public int LastModifiedBy { get; private set; } = 8;
    public int Revision { get; private set; } = 1;

    public void UpdateOwner(int userKey)
    {
        if (Key == 0)
        {
            this.CreatedOn = this.LastModifiedOn = DateTime.UtcNow;
            this.CreatedBy = this.LastModifiedBy = userKey;
        }
        else
        {
            this.LastModifiedBy = userKey;
            this.LastModifiedOn = DateTime.UtcNow;
            this.Revision++;
        }
    }

    public void UpdateDate(int userKey)
    {
        this.LastModifiedOn = DateTime.UtcNow;
        this.LastModifiedBy = userKey;
        this.Revision++;
    }

    public void SetRevision(int revision)
    {
        this.Revision = revision;
    }
}
