using System;
using System.IO;
using ReactiveUI;

namespace Shimmer.WiXUi
{
    public class FileLogger : IRxUILogger
    {
        readonly string filePath;
        readonly string messageFormat;
        readonly string directoryPath;

        static readonly object _lock = 42;

        public FileLogger(string appName)
        {
            var fileName = String.Format("{0}.txt", appName);
            directoryPath = Path.Combine(
                                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                                appName);
            filePath = Path.Combine(directoryPath, fileName);

            messageFormat = "{0} | {1} | {2}";
        }

        public void Write(string message, LogLevel logLevel)
        {
            if ((int) logLevel < (int) Level) return;

            lock (_lock) {
                try {
                    Directory.CreateDirectory(directoryPath); // if it exists, does nothing
                    using (var writer = new StreamWriter(filePath, true)) {
                        var now = DateTime.Now;
                        writer.WriteLine(
                            messageFormat, 
                            logLevel.ToString().ToUpper(),
                            now.ToString("yyyy-MM-dd hh:mm:ss tt zz"),
                            message);
                    }
                }
                catch {
                    // we're kinda screwed
                }
            }
        }

        public LogLevel Level { get; set; }
    }
}