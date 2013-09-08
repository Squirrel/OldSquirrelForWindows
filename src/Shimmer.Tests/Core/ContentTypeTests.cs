using System.IO;
using System.Xml;
using Shimmer.Core;
using Shimmer.Tests.TestHelpers;
using Xunit;

namespace Shimmer.Tests.Core
{
    public class ContentTypeTests
    {
        [Fact]
        public void SimpleFileIsProcessed()
        {
            var contentType = IntegrationTestHelper.GetPath("fixtures", "content-types", "basic.xml");
            var tempFile = Path.GetTempFileName() + ".xml";

            try {
                File.Copy(contentType, tempFile);

                var doc = new XmlDocument();
                doc.Load(tempFile);

                Assert.DoesNotThrow(() => ContentType.Merge(doc));
            } finally {
                File.Delete(tempFile);
            }
        }

        [Fact]
        public void ComplexFileIsProcessed()
        {
            var contentType = IntegrationTestHelper.GetPath("fixtures", "content-types", "complex.xml");
            var tempFile = Path.GetTempFileName() + ".xml";

            try
            {
                File.Copy(contentType, tempFile);

                var doc = new XmlDocument();
                doc.Load(tempFile);

                Assert.DoesNotThrow(() => ContentType.Merge(doc));
            }
            finally
            {
                File.Delete(tempFile);
            }
        }
    }
}
