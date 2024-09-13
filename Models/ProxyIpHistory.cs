// Decompiled with JetBrains decompiler
// Type: ExposeLocalhostNet.Models.ProxyIpHistory
// Assembly: localtonet, Version=3.1.0.0, Culture=neutral, PublicKeyToken=null
// MVID: 6B72F1D6-F66A-4D35-B220-62CCCFE10982
// Assembly location: localtonet.dll inside D:\AppicLogics\localtonet.exe)

using System;

#nullable disable
namespace ExposeLocalhostNet.Models
{
  public class ProxyIpHistory
  {
    public string UserToken { get; set; }

    public string IP { get; set; }

    public DateTime Date { get; set; }

    public bool IsHealth { get; set; }
  }
}
