// Decompiled with JetBrains decompiler
// Type: ExposeLocalhostNet.Models.FileServer.SettingFileServer
// Assembly: localtonet, Version=3.1.0.0, Culture=neutral, PublicKeyToken=null
// MVID: 6B72F1D6-F66A-4D35-B220-62CCCFE10982
// Assembly location: localtonet.dll inside D:\AppicLogics\localtonet.exe)

using System.Collections.Concurrent;

#nullable disable
namespace ExposeLocalhostNet.Models.FileServer
{
  public class SettingFileServer
  {
    public SettingFileServer()
    {
      this.UserRoleFileServer = new ConcurrentDictionary<string, ExposeLocalhostNet.Models.FileServer.UserRoleFileServer>();
    }

    public string Title { get; set; }

    public bool CanEveryoneReadAccess { get; set; }

    public bool CanEveryoneWriteAccess { get; set; }

    public bool CanEveryoneDeleteAccess { get; set; }

    public ConcurrentDictionary<string, ExposeLocalhostNet.Models.FileServer.UserRoleFileServer> UserRoleFileServer { get; set; }
  }
}
