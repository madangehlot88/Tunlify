// Decompiled with JetBrains decompiler
// Type: ExposeLocalhostNet.Models.RequestLog
// Assembly: localtonet, Version=3.1.0.0, Culture=neutral, PublicKeyToken=null
// MVID: 6B72F1D6-F66A-4D35-B220-62CCCFE10982
// Assembly location: localtonet.dll inside D:\AppicLogics\localtonet.exe)

using System;

#nullable disable
namespace ExposeLocalhostNet.Models
{
  public class RequestLog
  {
    public Guid UserRelayGuidId { get; set; }

    public DateTime DateTime { get; set; }

    public string RequestIp { get; set; }

    public string ClientIp { get; set; }

    public string Destination { get; set; }

    public string TokenName { get; set; }

    public string ProtocolType { get; set; }
  }
}
