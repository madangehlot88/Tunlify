// Decompiled with JetBrains decompiler
// Type: ExposeLocalhostNet.Models.FileInfoDetail
// Assembly: localtonet, Version=3.1.0.0, Culture=neutral, PublicKeyToken=null
// MVID: 6B72F1D6-F66A-4D35-B220-62CCCFE10982
// Assembly location: localtonet.dll inside D:\AppicLogics\localtonet.exe)

using System;

#nullable disable
namespace ExposeLocalhostNet.Models
{
  public class FileInfoDetail
  {
    public FileInfoDetail(
      string name,
      string path,
      long length,
      DateTime lastWriteTime,
      bool isFolder = false)
    {
      this.Name = name;
      this.Path = this.SetPath(isFolder, path, name);
      this.Length = this.DataLengthCalc(length);
      this.LastWriteTime = lastWriteTime;
      this.IsFolder = isFolder;
    }

    public string Name { get; set; }

    public string Path { get; set; }

    public string Length { get; set; }

    public bool IsFolder { get; set; }

    public DateTime LastWriteTime { get; set; }

    private string SetPath(bool isFolder, string lastPath, string name)
    {
      string str = ("/" + lastPath + "/" + name).Replace("//", "/");
      if (string.IsNullOrEmpty(lastPath))
        str = !isFolder ? name ?? "" : name + "/";
      return str;
    }

    private string DataLengthCalc(long length)
    {
      if (length <= 0L)
        return string.Empty;
      string[] strArray = new string[5]
      {
        "Byte",
        "Kb",
        "Mb",
        "Gb",
        "Tb"
      };
      double num = (double) length * 1.0;
      int index;
      for (index = 0; num > 1024.0 && index < strArray.Length; ++index)
        num /= 1024.0;
      return num.ToString("F2") + " " + strArray[index];
    }
  }
}
