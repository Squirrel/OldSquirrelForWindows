using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Text;
using System.Threading;
using NSync.Core;
using SharpBits.Base;

namespace NSync.Client
{
    public interface IUrlDownloader : IEnableLogger
    {
        IObservable<string> DownloadUrl(string url);
        IObservable<Unit> QueueBackgroundDownloads(IEnumerable<string> urls, IEnumerable<string> localPaths);
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

        public IObservable<Unit> QueueBackgroundDownloads(IEnumerable<string> urls, IEnumerable<string> localPaths)
        {
            var ret = new AsyncSubject<Unit>();
            var jobFiles = urls.Zip(localPaths, (url, path) => new {url, path});

            var job = manager.CreateJob(applicationName + "_" + Guid.NewGuid() , JobType.Download);
            job.OnJobError += (o, e) => ret.OnError(new BitsException(e.Error));
            job.OnJobTransferred += (o, e) => {
                try {
                    job.Complete();

                    if (job.ErrorCount != 0) {
                        ret.OnError(new BitsException(job.Error));
                    } else {
                        ret.OnNext(Unit.Default); 
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