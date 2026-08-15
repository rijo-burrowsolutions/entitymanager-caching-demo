// PURPOSE: the REAL ag-kit Office entity (copied from
// ag-kit/Modules/EntityManager/EntityManager.Domain/Entities/Office.cs).
using Ag.Abstractions.Entities;

namespace EntityManager.Domain.Entities;

public class Office : BaseEntity
{
    public int OfficeKey { get; set; }
    public string? ClientCode { get; set; }
    public string? OfficeName { get; set; }
    public string? BusinessHomepage { get; set; }
    public string? City { get; set; }
    public string? State { get; set; }
    public string? Country { get; set; }
    public string? ZipCode { get; set; }
    public string? Street { get; set; }
    public bool DisplayOnWebsite { get; set; }
    public string? Description { get; set; }
    public string? EmailAddress1 { get; set; }
    public string? Phone { get; set; }
    public string? Fax { get; set; }
    public string? FacebookUrl { get; set; }
    public string? InstagramUrl { get; set; }
    public string? LINKINURL { get; set; }
    public string? TwitterUrl { get; set; }
    public string? YoutubeUrl { get; set; }
    public string? PinterestUrl { get; set; }
    public decimal? IdcLatitude { get; set; }
    public decimal? IdcLongitude { get; set; }
    public string? SearchBy { get; set; }
    public string? Picture { get; set; }
    public int? ParentCompany { get; set; }
    public string? MlsAllow { get; set; }
    public string? RawJson { get; set; }
}
