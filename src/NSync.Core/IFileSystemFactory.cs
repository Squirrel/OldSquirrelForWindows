using System;
using System.Collections.Generic;
using System.IO.Abstractions;
using System.Linq;
using System.Reactive;
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
        IObservable<Unit> CopyAsync(string from, string to);
    }

    public class AnonFileSystem : IFileSystemFactory
    {
        Func<string, DirectoryInfoBase> getDirInfo;
        Func<string, FileInfoBase> getFileInfo;
        Func<string, FileBase> getFile;
        Func<string, DirectoryInfoBase> createDirRecursive;
        Action<string> deleteDirRecursive;
        Func<string, string, IObservable<Unit>> copyAsync;

        public AnonFileSystem(Func<string, DirectoryInfoBase> getDirInfo, 
            Func<string, FileInfoBase> getFileInfo, 
            Func<string, FileBase> getFile,
            Func<string, DirectoryInfoBase> createDirRecursive,
            Action<string> deleteDirRecursive,
            Func<string, string, IObservable<Unit>> copyAsync)
        {
            this.getDirInfo = getDirInfo;
            this.getFileInfo = getFileInfo;
            this.getFile = getFile;
            this.createDirRecursive = createDirRecursive;
            this.deleteDirRecursive = deleteDirRecursive;
            this.copyAsync = copyAsync;
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

        public IObservable<Unit> CopyAsync(string from, string to)
        {
            return copyAsync(from, to);
        }
    }
}