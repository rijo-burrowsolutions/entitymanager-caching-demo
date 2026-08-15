// PURPOSE: copied verbatim from the real ag-kit Ag.Abstractions - the
// envelope every List query returns (rows for the current page + metadata),
// so the real GetAgentListQuery/GetOfficeListQuery/GetCompanyListQuery
// handlers compile unchanged.
namespace Ag.Abstractions.Common;

public class PagedResult<T>
{
    public List<T> Items { get; set; } = [];
    public int TotalCount { get; set; }
    public int PageNumber { get; set; }
    public int PageSize { get; set; }
    public int TotalPages => PageSize <= 0 ? 0 : (int)Math.Ceiling(TotalCount / (double)PageSize);
}
