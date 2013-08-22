using System;
using System.IO;
using System.Threading;
using ReactiveUI;

namespace Shimmer.WiXUi
{
    public class FileLogger : IRxUILogger
    {
        readonly string filePath;
        readonly string messageFormat;
        readonly string directoryPath;

        static readonly ReaderWriterLock rwl = new ReaderWriterLock();

        public FileLogger(string appName)
        {
            var fileName = String.Format("{0}.txt", appName);
            directoryPath = Path.Combine(
                                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                                appName);
            filePath = Path.Combine(directoryPath, fileName);

            messageFormat = "{0} | {1} | {2} | {3}";
        }

        public void Write(string message, LogLevel logLevel)
        {
            if ((int)logLevel < (int)Level) return;

            try
            {
                rwl.AcquireWriterLock(5000);
                try
                {
                    Directory.CreateDirectory(directoryPath); // if it exists, does nothing
                    using (var writer = new StreamWriter(filePath, true))
                    {
                        var now = DateTime.Now;
                        writer.WriteLine(
                            messageFormat,
                            now.ToString("yyyy-MM-dd"),
                            now.ToString("hh:mm:ss tt zz"),
                            logLevel.ToString().ToUpper(),
                            message);
                    }
                }
                finally
                {
                    rwl.ReleaseWriterLock();
                }
            }
            catch (ApplicationException)
            {
                // swallow this exception
            }
        }

        public LogLevel Level { get; set; }
    }
}