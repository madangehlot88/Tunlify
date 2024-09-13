// Decompiled with JetBrains decompiler
// Type: ExposeLocalhostNet.Models.MyIp
// Assembly: localtonet, Version=3.1.0.0, Culture=neutral, PublicKeyToken=null
// MVID: 6B72F1D6-F66A-4D35-B220-62CCCFE10982
// Assembly location: localtonet.dll inside D:\AppicLogics\localtonet.exe)

using Newtonsoft.Json;

#nullable disable
namespace ExposeLocalhostNet.Models
{
  internal class MyIp
  {
    [JsonProperty("status")]
    public string Status { get; set; }

    [JsonProperty("country")]
    public string Country { get; set; }

    [JsonProperty("countryCode")]
    public string CountryCode { get; set; }

    [JsonProperty("region")]
    public string Region { get; set; }

    [JsonProperty("regionName")]
    public string RegionName { get; set; }

    [JsonProperty("city")]
    public string City { get; set; }

    [JsonProperty("zip")]
    public string Zip { get; set; }

    [JsonProperty("lat")]
    public double Lat { get; set; }

    [JsonProperty("lon")]
    public double Lon { get; set; }

    [JsonProperty("timezone")]
    public string Timezone { get; set; }

    [JsonProperty("isp")]
    public string Isp { get; set; }

    [JsonProperty("org")]
    public string Org { get; set; }

    [JsonProperty("as")]
    public string As { get; set; }

    [JsonProperty("query")]
    public string Query { get; set; }
  }
}
