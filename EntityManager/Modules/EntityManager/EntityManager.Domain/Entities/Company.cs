// PURPOSE: the REAL ag-kit Company entity (copied from
// ag-kit/Modules/EntityManager/EntityManager.Domain/Entities/Company.cs).
namespace EntityManager.Domain.Entities;

using Ag.Abstractions.Entities;

public class Company : BaseEntity
{
    public int CompanyKey { get; set; }
    public string? ClientCode { get; set; }
    public string? BlogUrl { get; set; }
    public string? City { get; set; }
    public string? CompanyName { get; set; }
    public string? CompanyUrl { get; set; }
    public string? EmailAddress1 { get; set; }
    public string? FacebookUrl { get; set; }
    public string? FbAppId { get; set; }
    public string? FbAppSecret { get; set; }
    public string? FbToken { get; set; }
    public string? FbUserId { get; set; }
    public string? FlickrUrl { get; set; }
    public decimal? IdcLatitude { get; set; }
    public decimal? IdcLongitude { get; set; }
    public string? InstagramUrl { get; set; }
    public string? LinkInUrl { get; set; }
    public string? MlsAllow { get; set; }
    public string? PhoneOffice { get; set; }
    public string? Picture { get; set; }
    public string? PinterestUrl { get; set; }
    public string? PlaxoUrl { get; set; }
    public string? State { get; set; }
    public string? Street { get; set; }
    public string? TwitterUrl { get; set; }
    public string? YelpUrl { get; set; }
    public string? YoutubeUrl { get; set; }
    public string? Zipcode { get; set; }
    public string? RawJson { get; set; }
}
