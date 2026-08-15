// PURPOSE: copied verbatim from the real ag-kit Ag.Util - fills in a
// default CDN picture URL (built from the entity's Guid) whenever the
// RawJson blob doesn't already have a "picture" field.
namespace Ag.Util.Json;

using System.Text.Json.Nodes;

public static class JsonHelper
{
    public static void AddDefaultPicture(JsonObject jsonObject, string cdnUrl, string recordtype, string? clientCode = "WRE")
    {
        if (jsonObject.ContainsKey("picture"))
            return;

        var guid = jsonObject["guid"]?.GetValue<string>();

        if (string.IsNullOrWhiteSpace(guid))
            return;
        if (recordtype == "AGENT")
        {
            jsonObject["picture"] = $"{cdnUrl.TrimEnd('/')}/{clientCode}_PUBLIC/image_cache/AGENT/PICTURE/4CC/{guid}_1.jpg";
        }
        else if (recordtype == "COMPANY")
        {
            jsonObject["picture"] = $"{cdnUrl.TrimEnd('/')}/{clientCode}_PUBLIC/image_cache/COMPANY/PICTURE/4CC/{guid}_1.jpg";
        }
        else if (recordtype == "OFFICE")
        {
            jsonObject["picture"] = $"{cdnUrl.TrimEnd('/')}/{clientCode}_PUBLIC/image_cache/OFFICE/PICTURE/A21/{guid}_1.jpg";
        }
    }
}
