using System;
using System.IO;

namespace NSync.Client
{
    public class UpdateManager
    {
        Func<string, Stream> openPath;
        Func<string, IObservable<string>> downloadUrl;

        public UpdateManager(string url, 
            Func<string, Stream> openPathMock = null,
            Func<string, IObservable<string>> downloadUrlMock = null)
        {
            openPath = openPathMock;
            downloadUrl = downloadUrlMock;
        }

        public IObservable<UpdateInfo> CheckForUpdate()
        {
            throw new NotImplementedException();
        }
    }

    public class UpdateInfo
    {
        public string Version { get; protected set; }
    }
}