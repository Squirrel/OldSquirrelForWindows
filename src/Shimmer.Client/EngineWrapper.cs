using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.Tools.WindowsInstallerXml.Bootstrapper;

namespace Shimmer.Client
{
    public interface IEngine
    {
        Engine.Variables<long> NumericVariables { get; }
        int PackageCount { get; }
        Engine.Variables<string> StringVariables { get; }
        Engine.Variables<Version> VersionVariables { get; }

        void Apply(IntPtr hwndParent);
        void CloseSplashScreen();
        void Detect();
        bool Elevate(IntPtr hwndParent);
        string EscapeString(string input);
        bool EvaluateCondition(string condition);
        string FormatString(string format);
        void Log(Microsoft.Tools.WindowsInstallerXml.Bootstrapper.LogLevel level, string message);
        void Plan(LaunchAction action);
        void SetLocalSource(string packageOrContainerId, string payloadId, string path);
        void SetDownloadSource(string packageOrContainerId, string payloadId, string url, string user, string password);
        int SendEmbeddedError(int errorCode, string message, int uiHint);
        int SendEmbeddedProgress(int progressPercentage, int overallPercentage);
        void Quit(int exitCode);
    }

    public class EngineWrapper : IEngine
    {
        readonly Engine inner;
        public EngineWrapper(Engine inner)
        {
            this.inner = inner;
        }

        public Engine.Variables<long> NumericVariables { get { return inner.NumericVariables; } }

        public int PackageCount { get { return inner.PackageCount; } }
        public Engine.Variables<string> StringVariables { get { return inner.StringVariables; } }
        public Engine.Variables<Version> VersionVariables { get { return inner.VersionVariables; } }

        public void Apply(IntPtr hwndParent)
        {
            inner.Apply(hwndParent);
        }

        public void CloseSplashScreen()
        {
            inner.CloseSplashScreen();
        }

        public void Detect()
        {
            inner.Detect();
        }

        public bool Elevate(IntPtr hwndParent)
        {
            return inner.Elevate(hwndParent);
        }

        public string EscapeString(string input)
        {
            return inner.EscapeString(input);
        }

        public bool EvaluateCondition(string condition)
        {
            return inner.EvaluateCondition(condition);
        }

        public string FormatString(string format)
        {
            return inner.FormatString(format);
        }

        public void Log(LogLevel level, string message)
        {
            inner.Log(level, message);
        }

        public void Plan(LaunchAction action)
        {
            inner.Plan(action);
        }

        public void SetLocalSource(string packageOrContainerId, string payloadId, string path)
        {
            inner.SetLocalSource(packageOrContainerId, payloadId, path);
        }

        public void SetDownloadSource(string packageOrContainerId, string payloadId, string url, string user, string password)
        {
            inner.SetDownloadSource(packageOrContainerId, payloadId, url, user, password);
        }

        public int SendEmbeddedError(int errorCode, string message, int uiHint)
        {
            return inner.SendEmbeddedError(errorCode, message, uiHint);
        }

        public int SendEmbeddedProgress(int progressPercentage, int overallPercentage)
        {
            return inner.SendEmbeddedProgress(progressPercentage, overallPercentage);
        }

        public void Quit(int exitCode)
        {
            inner.Quit(exitCode);
        }
    }
}
