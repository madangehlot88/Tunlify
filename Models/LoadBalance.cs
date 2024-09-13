// Decompiled with JetBrains decompiler
// Type: ExposeLocalhostNet.Models.LoadBalance
// Assembly: localtonet, Version=3.1.0.0, Culture=neutral, PublicKeyToken=null
// MVID: 6B72F1D6-F66A-4D35-B220-62CCCFE10982
// Assembly location: localtonet.dll inside D:\AppicLogics\localtonet.exe)

#nullable disable
namespace ExposeLocalhostNet.Models
{
  public class LoadBalance
  {
    public int Id { get; set; }

    public string Ip { get; set; }

    public int Port { get; set; }

    public double Percent { get; set; }

    public bool? IsHealthy { get; set; }

    public bool EnableHealthy { get; set; }

    public bool EnableHealthyCheckWithHttp { get; set; }

    public string Url { get; set; }

    public bool? IsSslStream { get; set; }
  }
}
