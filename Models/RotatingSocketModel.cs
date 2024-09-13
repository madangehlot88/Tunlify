// Decompiled with JetBrains decompiler
// Type: ExposeLocalhostNet.Models.RotatingSocketModel
// Assembly: localtonet, Version=3.1.0.0, Culture=neutral, PublicKeyToken=null
// MVID: 6B72F1D6-F66A-4D35-B220-62CCCFE10982
// Assembly location: localtonet.dll inside D:\AppicLogics\localtonet.exe)

#nullable disable
namespace ExposeLocalhostNet.Models
{
  public class RotatingSocketModel
  {
    public string SessionId { get; set; }

    public string Host { get; set; }

    public int Port { get; set; }

    public string FistRequestString { get; set; }

    public string Ip { get; set; }

    public int Command { get; set; }
  }
}
