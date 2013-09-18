using System;
using System.Diagnostics;
using System.IO;
using ReactiveUI;

namespace Shimmer.WiXUi
{
    public class FileLogger : IRxUILogger
    {
        readonly string filePath;
        readonly string messageFormat;

        static readonly object _lock = 42;

        public FileLogger(string appName)
        {
            var id = Process.GetCurrentProcess().Id;
            var fileName = String.Format("{0}-{1}.txt", appName, id);
            filePath = Path.Combine(LogDirectory, fileName);
            messageFormat = "{0} | {1} | {2}";
        }

        public static string LogDirectory {
            get {
                return Path.Combine(
                               Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                               "Shimmer");
            }
        }
        
        public void Write(string message, LogLevel logLevel)
        {
            if ((int) logLevel < (int) Level) return;

            lock (_lock) {
                try {
                    Directory.CreateDirectory(LogDirectory); // if it exists, does nothing
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