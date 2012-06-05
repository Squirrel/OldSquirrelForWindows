using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reactive.Linq;
using System.Text;
using NSync.Client;
using NSync.Tests.TestHelpers;
using ReactiveUI;
using Xunit;

namespace NSync.Tests.Client
{
    public class BitsManagerTests
    {
        [Fact]
        public void BitsDownloadsSomeUrls()
        {
            var urls = new[] {
                "https://a248.e.akamai.net/assets.github.com/images/modules/about_page/octocat.png",
                "https://a248.e.akamai.net/assets.github.com/images/modules/about_page/github_logo.png",
            };

            var files = new[] {
                "octocat.png",
                "gh_logo.png",
            };

            string tempPath = null;
            using (IntegrationTestHelper.WithTempDirectory(out tempPath))
            using (var fixture = new BitsUrlDownloader("BITSTests")) {
                fixture.QueueBackgroundDownloads(urls, files.Select(x => Path.Combine(tempPath, x)))
                    .Timeout(TimeSpan.FromSeconds(120), RxApp.TaskpoolScheduler)
                    .First();

                files.Select(x => Path.Combine(tempPath, x))
                    .Select(x => new FileInfo(x))
                    .ForEach(x => {
                        x.Exists.ShouldBeTrue(); 
                        x.Length.ShouldNotEqual(0);
                    });
            }
        }

        [Fact]
        public void BitsFailsOnGarbageUrls()
        {
            var urls = new[] {
                "https://example.com/nothere.png",
                "https://a248.e.akamai.net/assets.github.com/images/modules/about_page/github_logo.png",
            };

            var files = new[] {
                "octocat.png",
                "gh_logo.png",
            };

            string tempPath = null;
            using (IntegrationTestHelper.WithTempDirectory(out tempPath))
            using (var fixture = new BitsUrlDownloader("BITSTests")) {
                Assert.Throws<Exception>(() => {
                    fixture.QueueBackgroundDownloads(urls, files.Select(x => Path.Combine(tempPath, x)))
                        .Timeout(TimeSpan.FromSeconds(120), RxApp.TaskpoolScheduler)
                        .First();
                });
            }
        }
    }
}
