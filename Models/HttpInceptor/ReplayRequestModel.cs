// Decompiled with JetBrains decompiler
// Type: ExposeLocalhostNet.Models.HttpInceptor.ReplayRequestModel
// Assembly: localtonet, Version=3.1.0.0, Culture=neutral, PublicKeyToken=null
// MVID: 6B72F1D6-F66A-4D35-B220-62CCCFE10982
// Assembly location: localtonet.dll inside D:\AppicLogics\localtonet.exe)

using System;
using System.Collections.Generic;

#nullable disable
namespace ExposeLocalhostNet.Models.HttpInceptor
{
  public class ReplayRequestModel
  {
    public Guid Id { get; set; }

    public Guid UserRelayGuidId { get; set; }

    public Dictionary<string, string> Headers { get; set; } = new Dictionary<string, string>();

    public byte[] Body { get; set; }

    public string BodyString { get; set; }

    public string Method { get; set; }

    public string Url { get; set; }

    public bool IsModifiedRequest { get; set; }
  }
}
