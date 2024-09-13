// Decompiled with JetBrains decompiler
// Type: ExposeLocalhostNet.Models.FileServerIndexModel
// Assembly: localtonet, Version=3.1.0.0, Culture=neutral, PublicKeyToken=null
// MVID: 6B72F1D6-F66A-4D35-B220-62CCCFE10982
// Assembly location: localtonet.dll inside D:\AppicLogics\localtonet.exe)

using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

#nullable disable
namespace ExposeLocalhostNet.Models
{
  public class FileServerIndexModel
  {
    public FileServerIndexModel()
    {
      this.FileInfoDetailList = new List<FileInfoDetail>();
      this.PathDic = new List<Tuple<string, string>>();
      this.FolderPathList = new List<Tuple<string, string>>();
    }

    public string Title { get; set; }

    public string UserName { get; set; }

    public string FaviconPath { get; set; }

    public string LogoFile { get; set; }

    public string WrongPasswordMessage { get; set; }

    public List<Tuple<string, string>> PathDic { get; set; }

    public List<FileInfoDetail> FileInfoDetailList { get; set; }

    public List<Tuple<string, string>> FolderPathList { get; set; }

    public bool IsAuth { get; set; }

    public bool CanCreateNewFolder { get; set; }

    public bool CanUploadFile { get; set; }

    public bool CanMoveToFolder { get; set; }

    public bool CanRename { get; set; }

    public bool CanDelete { get; set; }

    public bool HaveUserPermissions { get; set; }

    public bool CanEveryoneReadAccess { get; set; }

    public void SetPathDic(string lastPath)
    {
      string[] strArray = lastPath.Split('/', (StringSplitOptions) 0);
      List<Tuple<string, string>> tupleList = new List<Tuple<string, string>>();
      tupleList.Add(Tuple.Create<string, string>("", "Home"));
      if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
      {
        for (int index1 = 0; index1 < strArray.Length; ++index1)
        {
          if (!string.IsNullOrEmpty(strArray[index1]))
          {
            if (index1 > 0)
            {
              string str = string.Empty;
              for (int index2 = 0; index2 <= index1; ++index2)
                str = str + "/" + strArray[index2];
              tupleList.Add(Tuple.Create<string, string>(str, strArray[index1]));
            }
            else
              tupleList.Add(Tuple.Create<string, string>("/" + strArray[index1], strArray[index1]));
          }
        }
      }
      else
      {
        for (int index3 = 0; index3 < strArray.Length; ++index3)
        {
          if (!string.IsNullOrEmpty(strArray[index3]))
          {
            if (index3 > 0)
            {
              string str = string.Empty;
              for (int index4 = 0; index4 <= index3; ++index4)
                str = str + "/" + strArray[index4];
              if (str.StartsWith("//"))
                str = str.TrimStart('/');
              tupleList.Add(Tuple.Create<string, string>("/" + str, strArray[index3]));
            }
            else
            {
              string str = "/" + strArray[index3];
              if (strArray[index3].StartsWith("//"))
                str = str.TrimStart('/');
              tupleList.Add(Tuple.Create<string, string>("/" + str, strArray[index3]));
            }
          }
        }
      }
      this.PathDic = tupleList;
    }
  }
}
