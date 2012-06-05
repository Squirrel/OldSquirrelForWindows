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
        DirectoryInfoBase CreateDirectoryRecursive(string path);
        void DeleteDirectoryRecursive(string path);
    }

    public class AnonFileSystem : IFileSystemFactory
    {
        Func<string, DirectoryInfoBase> getDirInfo;
        Func<string, FileInfoBase> getFileInfo;
        Func<string, FileBase> getFile;
        Func<string, DirectoryInfoBase> createDirRecursive;
        Action<string> deleteDirRecursive;

        public AnonFileSystem(Func<string, DirectoryInfoBase> getDirectoryInfo, 
            Func<string, FileInfoBase> getFileInfo, 
            Func<string, FileBase> getFile,
            Func<string, DirectoryInfoBase> createDirRecursive,
            Action<string> deleteDirRecursive)
        {
            this.getDirInfo = getDirectoryInfo;
            this.getFileInfo = getFileInfo;
            this.getFile = getFile;
            this.createDirRecursive = createDirRecursive;
            this.deleteDirRecursive = deleteDirRecursive;
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

        public void DeleteDirectoryRecursive(string path)
        {
            deleteDirRecursive(path);
        }
    }
}