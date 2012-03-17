using System;
using Caliburn.Micro;
using Microsoft.Tools.WindowsInstallerXml.Bootstrapper;

namespace SampleApp.BA
{
    public class WixLog : ILog
    {
        private readonly Engine engine;
        private readonly Type t;

        public WixLog(Engine engine, Type t)
        {
            this.t = t;
            this.engine = engine;
        }

        public void Error(Exception exception)
        {
            engine.Log(LogLevel.Error, exception.Message);
        }

        public void Info(string format, params object[] args)
        {
            engine.Log(LogLevel.Verbose, String.Format("[CM {0}] - {1}", t.Name, String.Format(format, args)));
        }

        public void Warn(string format, params object[] args)
        {
            engine.Log(LogLevel.Standard, String.Format("[CM {0}] - {1}", t.Name, String.Format(format, args)));
        }
    }
}
