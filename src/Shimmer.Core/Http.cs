using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Text;
using System.Diagnostics.Contracts;

namespace Shimmer.Core
{
    public static class Http
    {
        /// <summary>
        /// Download data from an HTTP URL and insert the result into the
        /// cache. If the data is already in the cache, this returns
        /// a cached value. The URL itself is used as the key.
        /// </summary>
        /// <param name="url">The URL to download.</param>
        /// <param name="headers">An optional Dictionary containing the HTTP
        /// request headers.</param>
        /// <returns>The data downloaded from the URL.</returns>
        public static IObservable<byte[]> DownloadUrl(string url, Dictionary<string, string> headers = null)
        {
            Contract.Requires(url != null);

            var ret = makeWebRequest(new Uri(url), headers)
                .SelectMany(processAndCacheWebResponse)
                .Multicast(new AsyncSubject<byte[]>());

            ret.Connect();
            return ret;
        }

        static IObservable<byte[]> processAndCacheWebResponse(WebResponse wr)
        {
            var hwr = (HttpWebResponse)wr;
            if ((int)hwr.StatusCode >= 400) {
                return Observable.Throw<byte[]>(new WebException(hwr.StatusDescription));
            }

            var ms = new MemoryStream();
            hwr.GetResponseStream().CopyTo(ms);

            var ret = ms.ToArray();
            return Observable.Return(ret);
        }

        static IObservable<WebResponse> makeWebRequest(
            Uri uri,
            Dictionary<string, string> headers = null,
            string content = null,
            int retries = 3,
            TimeSpan? timeout = null)
        {
            var request = Observable.Defer(() => {
                var hwr = WebRequest.Create(uri);
                if (headers != null) {
                    foreach (var x in headers) {
                        hwr.Headers[x.Key] = x.Value;
                    }
                }

                if (content == null) {
                    return Observable.FromAsyncPattern<WebResponse>(hwr.BeginGetResponse, hwr.EndGetResponse)();
                }

                var buf = Encoding.UTF8.GetBytes(content);
                return Observable.FromAsyncPattern<Stream>(hwr.BeginGetRequestStream, hwr.EndGetRequestStream)()
                    .SelectMany(x => Observable.FromAsyncPattern<byte[], int, int>(x.BeginWrite, x.EndWrite)(buf, 0, buf.Length))
                    .SelectMany(_ => Observable.FromAsyncPattern<WebResponse>(hwr.BeginGetResponse, hwr.EndGetResponse)());
            });

            return request.Timeout(timeout ?? TimeSpan.FromSeconds(15)).Retry(retries);
        }
    }
}