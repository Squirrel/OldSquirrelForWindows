using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NLog;
using NLog.Config;
using NLog.Targets;
using ReactiveUI;
using LogLevel = NLog.LogLevel;

namespace NSync.Core
{
    public interface IEnableLogger { }

    public static class LoggerMixin
    {
        static LoggerMixin()
        {
            if (RxApp.InUnitTestRunner())
            {
                var target = new ConsoleTarget() { Layout = "${level:uppercase=true} ${logger}: ${message}${onexception:inner=${newline}${exception:format=tostring}}" };
                var config = new LoggingConfiguration();

                config.LoggingRules.Add(new LoggingRule("*", LogLevel.Info, target));
                LogManager.Configuration = config;
            }
        }

        public static Logger Log<T>(this T This) where T : IEnableLogger
        {
            return LogManager.GetLogger(typeof(T).FullName);
        }
    }
}
