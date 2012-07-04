using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Text;
using System.Threading;
using ReactiveUI;
using SharpBits.Base;
using Shimmer.Core;

namespace Shimmer.Client
{
    public interface IUrlDownloader : IEnableLogger
    {
        IObservable<string> DownloadUrl(string url);
        IObservable<int> QueueBackgroundDownloads(IEnumerable<string> urls, IEnumerable<string> localPaths);
    }

    [Serializable]
    public sealed class DirectUrlDownloader : IUrlDownloader, IDisposable
    {
        readonly IFileSystemFactory fileSystem;

        public DirectUrlDownloader(IFileSystemFactory fileSystem)
        {
            this.fileSystem = fileSystem ?? AnonFileSystem.Default;
        }

        public IObservable<string> DownloadUrl(string url)
        {
            return Http.DownloadUrl(url).Select(x => Encoding.UTF8.GetString(x));
        }

        public IObservable<int> QueueBackgroundDownloads(IEnumerable<string> urls, IEnumerable<string> localPaths)
        {
            var fileCount = urls.Count();
            double toIncrement = 100.0 / fileCount;

            return urls.Zip(localPaths, (u, p) => new { Url = u, Path = p }).ToObservable()
                .Select(x => Http.DownloadUrl(x.Url).Select(Content => new { x.Url, x.Path, Content })).Merge(4)
                .SelectMany(x => Observable.Start(() => {
                    var fi = fileSystem.GetFileInfo(x.Path);
                    if (fi.Exists) fi.Delete();

                    using (var of = fi.OpenWrite()) {
                        of.Write(x.Content, 0, x.Content.Length);
                    }
                }, RxApp.TaskpoolScheduler))
                .Scan(0.0, (acc, _) => acc + toIncrement)
                .Select(x => (int)x);
        }

        public void Dispose()
        {
        }
    }

    public class BitsException : Exception
    {
        public BitsError Error { get; protected set; }

        public BitsException(BitsError error)
        {
            Error = error;
        }
    }

    public sealed class BitsUrlDownloader : IUrlDownloader, IDisposable
    {
        BitsManager manager;
        readonly string applicationName;

        public BitsUrlDownloader(string applicationName)
        {
            manager = new BitsManager();
            this.applicationName = applicationName;
        }

        public IObservable<string> DownloadUrl(string url)
        {
            return Http.DownloadUrl(url).Select(x => Encoding.UTF8.GetString(x));
        }

        public IObservable<int> QueueBackgroundDownloads(IEnumerable<string> urls, IEnumerable<string> localPaths)
        {
            var ret = new ReplaySubject<int>();
            var jobFiles = urls.Zip(localPaths, (url, path) => new {url, path});

            var job = manager.CreateJob(applicationName + "_" + Guid.NewGuid() , JobType.Download);
            job.OnJobError += (o, e) => ret.OnError(new BitsException(e.Error));
            job.OnJobTransferred += (o, e) => {
                try {
                    job.Complete();

                    if (job.ErrorCount != 0) {
                        ret.OnError(new BitsException(job.Error));
                    } else {
                        ret.OnNext(100); 
                        ret.OnCompleted();
                    }
                } catch (Exception ex) {
                    ret.OnError(ex);
                }
            };

            jobFiles
                .Where(x => !File.Exists(x.path))
                .ForEach(x => job.AddFile(x.url, x.path));

            job.Resume();

            return ret;
        }

        public void Dispose()
        {
            var mgr = Interlocked.Exchange(ref manager, null);
            if (mgr != null) {
                mgr.Dispose();
            }
        }
    }
}