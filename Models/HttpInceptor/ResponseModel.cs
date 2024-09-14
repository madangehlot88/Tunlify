﻿// Decompiled with JetBrains decompiler
// Type: ExposeLocalhostNet.Models.HttpInceptor.ResponseModel
// Assembly: localtonet, Version=3.1.0.0, Culture=neutral, PublicKeyToken=null
// MVID: 6B72F1D6-F66A-4D35-B220-62CCCFE10982
// Assembly location: localtonet.dll inside D:\AppicLogics\localtonet.exe)

using System;
using System.Collections.Generic;
using System.Text;

#nullable disable
namespace ExposeLocalhostNet.Models.HttpInceptor
{
  public class ResponseModel
  {
    public ResponseModel() => this.Headers = new Dictionary<string, string>();

    public Guid Id { get; set; }

    public Guid UserRelayGuidId { get; set; }

    public Dictionary<string, string> Headers { get; set; }

    public int StatusCode { get; set; }

    public string StatusDescription { get; set; }

    public string HeaderText { get; set; }

    public bool HasBody { get; set; }

    public string BodyString { get; set; }

    public string BodyHex { get; set; }

    public string Method { get; set; }

    public bool KeepBody { get; set; }

    public bool UpgradeToWebSocket { get; set; }

    public string ContentEncoding { get; set; }

    public long ContentLength { get; set; }

    public string ContentType { get; set; }

    public Encoding Encoding { get; set; }

    public bool KeepAlive { get; set; }

    public string HttpVersion { get; set; }

    public bool IsChunked { get; set; }

    public bool IsBodyRead { get; set; }

    public string ResponseTime { get; set; }
  }
}