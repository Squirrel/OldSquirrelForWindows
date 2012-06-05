using System;
using System.Collections.Generic;
using System.IO.Abstractions;
using System.Linq;
using System.Text;

namespace NSync.Core
{
    public interface IFileSystemFactory
    {
        DirectoryInfoBase GetDirectoryInfo(string path);
        FileInfoBase GetFileInfo(string path);
        FileBase GetFile(string path);
        DirectoryInfoBase CreateDirectoryRecursive(string combine);
    }

    public class AnonFileSystem : IFileSystemFactory
    {
        Func<string, DirectoryInfoBase> getDirInfo;
        Func<string, FileInfoBase> getFileInfo;
        Func<string, FileBase> getFile;
        Func<string, DirectoryInfoBase> createDirRecursive;

        public AnonFileSystem(Func<string, DirectoryInfoBase> getDirectoryInfo, 
            Func<string, FileInfoBase> getFileInfo, 
            Func<string, FileBase> getFile,
            Func<string, DirectoryInfoBase> createDirRecursive)
        {
            this.getDirInfo = getDirectoryInfo;
            this.getFileInfo = getFileInfo;
            this.getFile = getFile;
            this.createDirRecursive = createDirRecursive;
        }

        public DirectoryInfoBase GetDirectoryInfo(string path)
        {
            return getDirInfo(path);
        }

        public FileInfoBase GetFileInfo(string path)
        {
            return getFileInfo(path);
        }

        public FileBase GetFile(string path)
        {
            return getFile(path);
        }

        public DirectoryInfoBase CreateDirectoryRecursive(string path)
        {
            return createDirRecursive(path);
        }
    }
}