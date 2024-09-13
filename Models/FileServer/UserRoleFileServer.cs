// Decompiled with JetBrains decompiler
// Type: ExposeLocalhostNet.Models.FileServer.UserRoleFileServer
// Assembly: localtonet, Version=3.1.0.0, Culture=neutral, PublicKeyToken=null
// MVID: 6B72F1D6-F66A-4D35-B220-62CCCFE10982
// Assembly location: localtonet.dll inside D:\AppicLogics\localtonet.exe)

#nullable disable
namespace ExposeLocalhostNet.Models.FileServer
{
  public class UserRoleFileServer
  {
    public UserRoleFileServer(
      bool canReadAccess,
      bool canUploadFile,
      bool canDelete,
      string password)
    {
      this.CanReadAccess = canReadAccess;
      this.CanWriteAccess = canUploadFile;
      this.CanDeleteAccess = canDelete;
      this.Password = password;
    }

    public bool CanReadAccess { get; set; }

    public bool CanWriteAccess { get; set; }

    public bool CanDeleteAccess { get; set; }

    public string Password { get; set; }
  }
}
