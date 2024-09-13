// Decompiled with JetBrains decompiler
// Type: ExposeLocalhostNet.Models.FileServer.IsAuthResponseModel
// Assembly: localtonet, Version=3.1.0.0, Culture=neutral, PublicKeyToken=null
// MVID: 6B72F1D6-F66A-4D35-B220-62CCCFE10982
// Assembly location: localtonet.dll inside D:\AppicLogics\localtonet.exe)

#nullable disable
namespace ExposeLocalhostNet.Models.FileServer
{
  public class IsAuthResponseModel
  {
    public string Title { get; set; }

    public bool IsAuth { get; set; }

    public string UserName { get; set; }

    public bool CanCreateNewFolder { get; set; }

    public bool CanUploadFile { get; set; }

    public bool CanMoveToFolder { get; set; }

    public bool CanRename { get; set; }

    public bool CanDelete { get; set; }
  }
}
