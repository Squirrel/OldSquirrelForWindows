using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Text;
using System.Threading;
using ReactiveUI;

namespace Shimmer.Tests
{
    public sealed class StaticHttpServer : IDisposable
    {
        public int Port { get; protected set; }
        public string RootPath { get; protected set; }
        
        IDisposable inner;

        public StaticHttpServer(int port, string rootPath)
        {
            Port = port; RootPath = rootPath;
        }

        public IDisposable Start()
        {
            if (inner != null) {
                throw new InvalidOperationException("Already started!");
            }

            var server = new HttpListener();
            server.Prefixes.Add(String.Format("http://+:{0}/", Port));
            server.Start();

            var listener = Observable.Defer(() => Observable.FromAsyncPattern<HttpListenerContext>(server.BeginGetContext, server.EndGetContext)())
                .Repeat()
                .Subscribe(x => {
                    if (x.Request.HttpMethod != "GET") {
                        x.Response.StatusCode = 400;
                        using (var sw = new StreamWriter(x.Response.OutputStream, Encoding.UTF8)) {
                            sw.WriteLine("GETs only");
                        }
                        x.Response.Close();
                        return;
                    }

                    var target = Path.Combine(RootPath, x.Request.Url.AbsolutePath.Replace('/', Path.DirectorySeparatorChar).Substring(1));
                    var fi = new FileInfo(target);

                    if (!fi.FullName.StartsWith(RootPath)) {
                        x.Response.StatusCode = 401;
                        using (var sw = new StreamWriter(x.Response.OutputStream, Encoding.UTF8)) {
                            sw.WriteLine("Not Authorized");
                        }
                        x.Response.Close();
                        return;
                    }

                    if (!fi.Exists) {
                        x.Response.StatusCode = 404;
                        using (var sw = new StreamWriter(x.Response.OutputStream, Encoding.UTF8)) {
                            sw.WriteLine("Not Found");
                        }
                        x.Response.Close();
                        return;
                    }

                    try {
                        using (var input = File.OpenRead(target)) {
                            x.Response.StatusCode = 200;
                            input.CopyTo(x.Response.OutputStream);
                            x.Response.Close();
                        }
                    } catch (Exception ex) {
                        x.Response.StatusCode = 500;
                        using (var sw = new StreamWriter(x.Response.OutputStream, Encoding.UTF8)) {
                            sw.WriteLine(ex);
                        }
                        x.Response.Close();
                    }
                });

            var ret = Disposable.Create(() => {
                listener.Dispose();
                server.Stop();
                inner = null;
            });

            inner = ret;
            return ret;
        }

        public void Dispose()
        {
            var toDispose = Interlocked.Exchange(ref inner, null);
            if (toDispose != null) {
                toDispose.Dispose();
            }
        }
    }
}
