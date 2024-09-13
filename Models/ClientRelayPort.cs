// Decompiled with JetBrains decompiler
// Type: ExposeLocalhostNet.Models.ClientRelayPort
// Assembly: localtonet, Version=3.1.0.0, Culture=neutral, PublicKeyToken=null
// MVID: 6B72F1D6-F66A-4D35-B220-62CCCFE10982
// Assembly location: localtonet.dll inside D:\AppicLogics\localtonet.exe)

using System;

#nullable disable
namespace ExposeLocalhostNet.Models
{
  public class ClientRelayPort
  {
    public Guid UserDomainGuidId { get; set; }

    public string UserRelayId { get; set; }

    public string ServerIp { get; set; }

    public string ServerIPv6 { get; set; }

    public int ServerPort { get; set; }

    public int LocaltonetAppPort { get; set; }

    public int ServerListenerPort { get; set; }

    public int ServerClientListenerPort { get; set; }

    public string ServerId { get; set; }

    public int Mtu { get; set; }

    public byte[] ServerPassword { get; set; }

    public string LocalIp { get; set; }

    public int Port { get; set; }

    public string Http { get; set; }

    public string Https { get; set; }

    public string ProtocolType { get; set; }

    public DateTime StartedTime { get; set; }

    public int? TotalMinutes { get; set; }

    public string ServerDomain { get; set; }

    public string Path { get; set; }

    public bool BasicAuthentication { get; set; }

    public string BasicAuthenticationUsername { get; set; }

    public string BasicAuthenticationPassword { get; set; }

    public long Ping { get; set; }

    public string TunnelStatus { get; set; }

    public bool? IsSslStream { get; set; }

    public bool IsLogOn { get; set; }
  }
}
