// Decompiled with JetBrains decompiler
// Type: ExposeLocalhostNet.Models.ServerInfo
// Assembly: localtonet, Version=3.1.0.0, Culture=neutral, PublicKeyToken=null
// MVID: 6B72F1D6-F66A-4D35-B220-62CCCFE10982
// Assembly location: localtonet.dll inside D:\AppicLogics\localtonet.exe)

#nullable disable
namespace ExposeLocalhostNet.Models
{
  public class ServerInfo
  {
    public string ServerId { get; set; }

    public int Mtu { get; set; }

    public string Credential { get; set; }

    public string EndPoint { get; set; }
  }
}
