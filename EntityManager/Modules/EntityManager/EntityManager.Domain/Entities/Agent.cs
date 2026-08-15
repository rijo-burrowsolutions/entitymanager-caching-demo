// PURPOSE: the REAL ag-kit Agent entity (copied from
// ag-kit/Modules/EntityManager/EntityManager.Domain/Entities/Agent.cs) -
// every field idc_ety's real Agent table has, including the RawJson blob
// the real API actually returns to callers. AgentDetail is the flattened
// projection GetAgentDetail() builds by joining Agent -> Office -> Company.
using Ag.Abstractions.Entities;

namespace EntityManager.Domain.Entities;

public class Agent : BaseEntity
{
    public string ClientCode { get; set; }
    public int AgentKey { get; set; }
    public bool IsDisplayedOnWebsite { get; set; }
    public string? BusinessHomePage { get; set; }
    public bool IsTeam { get; set; }
    public string? GivenName { get; set; }
    public string? MiddleName { get; set; }
    public string? SurName { get; set; }
    public string? FullName { get; set; }
    public string? EmailAddress1 { get; set; }
    public string? EmailAddress2 { get; set; }
    public int AgentRoles { get; set; }
    public string? MlsAllow { get; set; }
    public string? Street { get; set; }
    public string? City { get; set; }
    public string? State { get; set; }
    public string? ZipCode { get; set; }
    public string? Country { get; set; }
    public string? BusinessPhone { get; set; }
    public string? BusinessFax { get; set; }
    public string? MobilePhone { get; set; }
    public string? OtherTelephone { get; set; }
    public string? Picture { get; set; }
    public string? Biography { get; set; }
    public string? LicenseNo { get; set; }
    public string? ProfessionalTitle { get; set; }
    public string? PreferredName { get; set; }
    public string? TimeZone { get; set; }
    public string? SearchBy { get; set; }
    public string? AppLink { get; set; }
    public string? Languages { get; set; }
    public string? Designations { get; set; }
    public string? FacebookUrl { get; set; }
    public string? InstagramUrl { get; set; }
    public string? LinkinUrl { get; set; }
    public string? SnapChatUrl { get; set; }
    public string? TwitterUrl { get; set; }
    public string? YoutubeUrl { get; set; }
    public string? PinterestUrl { get; set; }
    public string? WeChatUrl { get; set; }
    public string? BlogUrl { get; set; }
    public string? TiktokUrl { get; set; }
    public string? FBMessengerUrl { get; set; }
    public int? ParentOffice { get; set; }
    public string? SeoName { get; set; }
    public int? LinkCompany { get; set; }
    public string? RawJson { get; set; }
}

public class AgentDetail
{
    public int AgentKey { get; set; }
    public string? SeoName { get; set; }
    public string? ClientCode { get; set; }

    public string RawJson { get; set; } = string.Empty;

    public string OfficeName { get; set; } = string.Empty;
    public string OfficeStreet { get; set; } = string.Empty;
    public string OfficeCity { get; set; } = string.Empty;
    public string OfficeState { get; set; } = string.Empty;
    public string OfficeCountry { get; set; } = string.Empty;
    public string OfficeZipcode { get; set; } = string.Empty;
    public string OfficePhone { get; set; } = string.Empty;

    public string CompanyName { get; set; } = string.Empty;
}
