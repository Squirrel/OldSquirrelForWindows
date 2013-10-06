using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Text;
using System.Threading;
using ReactiveUIMicro;
using SharpBits.Base;
using Squirrel.Core;

namespace Squirrel.Client
{
    public interface IUrlDownloader
    {
        IObservable<string> DownloadUrl(string url, IObserver<int> progress);
        IObservable<Unit> QueueBackgroundDownloads(IEnumerable<string> urls, IEnumerable<string> localPaths, IObserver<int> progress);
    }

    [Serializable]
    public sealed class DirectUrlDownloader : IUrlDownloader, IDisposable
    {
        readonly IFileSystemFactory fileSystem;

        public DirectUrlDownloader(IFileSystemFactory fileSystem)
        {
            this.fileSystem = fileSystem ?? AnonFileSystem.Default;
        }

        public IObservable<string> DownloadUrl(string url, IObserver<int> progress = null)
        {
            progress = progress ?? new Subject<int>();

            var ret = Http.DownloadUrl(url)
                .Catch<byte[], TimeoutException>(ex => {
                    // TODO: log this exception?
                    return Observable.Return(new byte[0]);
                })
                .Select(x =>
                {
                    using (var reader = new StreamReader
                        (new MemoryStream(x), Encoding.UTF8)) {
                        return reader.ReadToEnd();
                    }
                })
                .PublishLast();

            // NB: We don't actually have progress, fake it out
            ret.Select(_ => 100).Subscribe(progress);

            ret.Connect();
            return ret;
        }

        public IObservable<Unit> QueueBackgroundDownloads(IEnumerable<string> urls, IEnumerable<string> localPaths, IObserver<int> progress = null)
        {
            var fileCount = urls.Count();
            double toIncrement = 100.0 / fileCount;
            progress = progress ?? new Subject<int>();

            var ret = urls.Zip(localPaths, (u, p) => new { Url = u, Path = p }).ToObservable()
                .Select(x => Http.DownloadUrl(x.Url).Select(Content => new { x.Url, x.Path, Content }))
                .Merge(4)
                .SelectMany(x => Observable.Start(() => {
                        var fi = fileSystem.GetFileInfo(x.Path);
                        if (fi.Exists) fi.Delete();

                        using (var of = fi.OpenWrite()) {
                            of.Write(x.Content, 0, x.Content.Length);
                        }
                    }, RxApp.TaskpoolScheduler))
                .Multicast(new ReplaySubject<Unit>());

            ret.Scan(0.0, (acc, _) => acc + toIncrement).Select(x => (int) x).Subscribe(progress);

            ret.Connect();
            return ret.TakeLast(1).Select(_ => Unit.Default);
        }

        public void Dispose()
        {
        }
    }

    // NB: This code is hella wrong
#if FALSE
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
#endif
}
