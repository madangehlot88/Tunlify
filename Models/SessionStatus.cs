// Decompiled with JetBrains decompiler
// Type: ExposeLocalhostNet.Models.SessionStatus
// Assembly: localtonet, Version=3.1.0.0, Culture=neutral, PublicKeyToken=null
// MVID: 6B72F1D6-F66A-4D35-B220-62CCCFE10982
// Assembly location: localtonet.dll inside D:\AppicLogics\localtonet.exe)

using System;
using System.Collections.Concurrent;

#nullable disable
namespace ExposeLocalhostNet.Models
{
  public class SessionStatus
  {
    public SessionStatus() => this.UserTunnel = new ConcurrentDictionary<Guid, ClientRelayPort>();

    public bool SignalRIsConnected { get; set; }

    public ConcurrentDictionary<Guid, ClientRelayPort> UserTunnel { get; set; }

    public string UserSubscription { get; set; }

    public string AuthTokenName { get; set; }
  }
}
