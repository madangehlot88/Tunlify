// Decompiled with JetBrains decompiler
// Type: ExposeLocalhostNet.Models.UserInfo
// Assembly: localtonet, Version=3.1.0.0, Culture=neutral, PublicKeyToken=null
// MVID: 6B72F1D6-F66A-4D35-B220-62CCCFE10982
// Assembly location: localtonet.dll inside D:\AppicLogics\localtonet.exe)

#nullable disable
namespace ExposeLocalhostNet.Models
{
  public class UserInfo
  {
    public string Email { get; set; }

    public string Subscription { get; set; }

    public bool IsRegistered { get; set; }

    public bool IsWrongAuthToken { get; set; }

    public string AuthTokenName { get; set; }
  }
}
