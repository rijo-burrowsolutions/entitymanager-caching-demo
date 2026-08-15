// PURPOSE: copied verbatim from the real ag-kit Ag.Abstractions - the base
// that BaseEntity inherits from. [NotMapped] means PageSize/PageNumber are
// C# convenience properties only, never actual database columns.
namespace Ag.Abstractions.Command;

using System.ComponentModel.DataAnnotations.Schema;

public class Pagination
{
    [NotMapped]
    public int PageSize { get; set; } = 20;
    [NotMapped]
    public int? PageNumber { get; set; }
}
