using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Interop;
using Caliburn.Micro;
using Microsoft.Tools.WindowsInstallerXml.Bootstrapper;

namespace SampleApp.BA
{
    public enum InstallationState
    {
        Initializing,
        DetectedAbsent,
        DetectedPresent,
        DetectedNewer,
        Applying,
        Applied,
        Failed,
    }

    public class ShellViewModel : Screen
    {
        private const int ERROR_UserCancelled = 1223;
        private readonly BootstrapperApplication ba;
        private IntPtr hwnd;
        private Dictionary<string, int> downloadRetries;
        private LaunchAction plannedAction;

        public ShellViewModel(BootstrapperApplication ba)
        {
            this.DisplayName = "SampleApp Installer";

            this.ba = ba;
            downloadRetries = new Dictionary<string, int>();

            ba.DetectBegin += DetectBegin;
            ba.DetectPackageComplete += DetectedPackage;
            ba.DetectRelatedBundle += DetectedRelatedBundle;
            ba.DetectComplete += DetectComplete;

            ba.PlanPackageBegin += PlanPackageBegin;
            ba.PlanComplete += PlanComplete;

            ba.ApplyBegin += ApplyBegin;
            ba.ApplyComplete += ApplyComplete;

            ba.ResolveSource += ResolveSource;
            ba.Error += ExecuteError;

            ba.ExecuteMsiMessage += this.ExecuteMsiMessage;
            ba.ExecuteProgress += this.ApplyExecuteProgress;
            ba.Progress += this.ApplyProgress;
            ba.CacheAcquireProgress += this.CacheAcquireProgress;
            ba.CacheComplete += this.CacheComplete;
        }

        protected override void OnInitialize()
        {
            base.OnInitialize();

            ba.Engine.Detect();
        }

        public InstallationState PreApplyState { get; set; }
        public InstallationState State { get; set; }
        public bool Cancelled { get; set; }
        public bool Downgrade { get; set; }
        public string Message { get; set; }
        public int CacheProgress { get; set; }
        public int ExecuteProgress { get; set; }

        public int Progress { get { return (this.CacheProgress + this.ExecuteProgress) / 2; } }
        public bool ProgressEnabled { get { return this.State == InstallationState.Applying; } }
        public bool CompleteEnabled { get { return this.State == InstallationState.Applied; } }

        private void Plan(LaunchAction action)
        {
            this.plannedAction = action;
            this.hwnd = (Application.Current.MainWindow == null) ? IntPtr.Zero : new WindowInteropHelper(Application.Current.MainWindow).Handle;

            this.Cancelled = false;
            ba.Engine.Plan(action);
        }

        public static bool HResultSucceeded(int status)
        {
            return status >= 0;
        }

        #region Engine Events

        private void DetectBegin(object sender, DetectBeginEventArgs e)
        {
            this.State = InstallationState.Initializing;
        }

        private void DetectedPackage(object sender, DetectPackageCompleteEventArgs e)
        {
            // The Package ID from the Bootstrapper chain.
            if (e.PackageId.Equals("SampleApp", StringComparison.Ordinal))
            {
                this.State = (e.State == PackageState.Present) ? InstallationState.DetectedPresent : InstallationState.DetectedAbsent;
            }
        }

        private void DetectedRelatedBundle(object sender, DetectRelatedBundleEventArgs e)
        {
            if (e.Operation == RelatedOperation.Downgrade)
            {
                this.Downgrade = true;
            }
        }

        private void DetectComplete(object sender, DetectCompleteEventArgs e)
        {
            if (ba.Command.Action == LaunchAction.Uninstall)
            {
                ba.Engine.Log(LogLevel.Verbose, "Invoking automatic plan for uninstall");
                Execute.OnUIThread(() =>
                {
                    this.Plan(LaunchAction.Uninstall);
                });
            }
            else if (HResultSucceeded(e.Status))
            {
                if (this.Downgrade)
                {
                    // TODO: What behavior do we want for downgrade?
                    this.State = InstallationState.DetectedNewer;
                }

                // If we're not waiting for the user to click install, dispatch plan with the default action.
                if (ba.Command.Display != Display.Full)
                {
                    ba.Engine.Log(LogLevel.Verbose, "Invoking automatic plan for non-interactive mode.");
                    Execute.OnUIThread(() =>
                    {
                        this.Plan(ba.Command.Action);
                    });
                }
            }
            else
            {
                this.State = InstallationState.Failed;
            }
        }

        private void PlanPackageBegin(object sender, PlanPackageBeginEventArgs e)
        {
            // Turns off .NET install when setting up the install plan as we already have it.
            //if (e.PackageId.Equals(ba.Engine.StringVariables["WixMbaPrereqPackageId"], StringComparison.Ordinal))
            if (e.PackageId.Equals("Netfx4Full", StringComparison.Ordinal))
            {
                e.State = RequestState.None;
            }
        }

        private void PlanComplete(object sender, PlanCompleteEventArgs e)
        {
            if (HResultSucceeded(e.Status))
            {
                this.PreApplyState = this.State;
                this.State = InstallationState.Applying;
                ba.Engine.Apply(this.hwnd);
            }
            else
            {
                this.State = InstallationState.Failed;
            }
        }

        private void ApplyBegin(object sender, ApplyBeginEventArgs e)
        {
            this.downloadRetries.Clear();
        }

        private void ApplyComplete(object sender, ApplyCompleteEventArgs e)
        {
            // If we're not in Full UI mode, we need to alert the dispatcher to stop and close the window for passive.
            if (ba.Command.Display != Display.Full)
            {
                // If its passive, send a message to the window to close.
                if (ba.Command.Display == Display.Passive)
                {
                    ba.Engine.Log(LogLevel.Verbose, "Automatically closing the window for non-interactive install");
                    this.TryClose();
                }
                else
                {
                    Application.Current.Shutdown();
                }
            }

            // Set the state to applied or failed unless the state has already been set back to the preapply state
            // which means we need to show the UI as it was before the apply started.
            if (this.State != this.PreApplyState)
            {
                this.State = HResultSucceeded(e.Status) ? InstallationState.Applied : InstallationState.Failed;
            }
        }

        private void ResolveSource(object sender, ResolveSourceEventArgs e)
        {
            int retries = 0;

            this.downloadRetries.TryGetValue(e.PackageOrContainerId, out retries);
            this.downloadRetries[e.PackageOrContainerId] = retries + 1;

            e.Result = retries < 3 && !String.IsNullOrEmpty(e.DownloadSource) ? Result.Download : Result.Ok;
        }

        private void ExecuteError(object sender, ErrorEventArgs e)
        {
            lock (this)
            {
                if (!this.Cancelled)
                {
                    // If the error is a cancel coming from the engine during apply we want to go back to the preapply state.
                    if (this.State == InstallationState.Applying && e.ErrorCode == ERROR_UserCancelled)
                    {
                        this.State = this.PreApplyState;
                    }
                    else
                    {
                        this.Message = e.ErrorMessage;
                        Execute.OnUIThread(() =>
                        {
                            MessageBox.Show(Application.Current.MainWindow, e.ErrorMessage, "WiX Toolset", MessageBoxButton.OK, MessageBoxImage.Error);
                        });
                    }
                }

                e.Result = this.Cancelled ? Result.Cancel : Result.Ok;
            }
        }

        private void ExecuteMsiMessage(object sender, ExecuteMsiMessageEventArgs e)
        {
            lock (this)
            {
                this.Message = e.Message;
                e.Result = this.Cancelled ? Result.Cancel : Result.Ok;
            }
        }

        private void ApplyProgress(object sender, ProgressEventArgs e)
        {
            lock (this)
            {
                e.Result = this.Cancelled ? Result.Cancel : Result.Ok;
            }
        }

        private void CacheAcquireProgress(object sender, CacheAcquireProgressEventArgs e)
        {
            lock (this)
            {
                this.CacheProgress = e.OverallPercentage;
                e.Result = this.Cancelled ? Result.Cancel : Result.Ok;
            }
        }

        private void CacheComplete(object sender, CacheCompleteEventArgs e)
        {
            lock (this)
            {
                this.CacheProgress = 100;
            }
        }

        private void ApplyExecuteProgress(object sender, ExecuteProgressEventArgs e)
        {
            lock (this)
            {
                this.ExecuteProgress = e.OverallPercentage;

                if (ba.Command.Display == Display.Embedded)
                {
                    ba.Engine.SendEmbeddedProgress(e.ProgressPercentage, this.Progress);
                }

                e.Result = this.Cancelled ? Result.Cancel : Result.Ok;
            }
        }

        #endregion

        #region Actions

        public bool CanInstall { get { return this.State == InstallationState.DetectedAbsent; } }
        public void Install()
        {
            this.Plan(LaunchAction.Install);
        }

        public bool CanRepair { get { return this.State == InstallationState.DetectedPresent; } }
        public void Repair()
        {
            this.Plan(LaunchAction.Repair);
        }

        public bool CanUninstall { get { return this.State == InstallationState.DetectedPresent; } }
        public void Uninstall()
        {
            this.Plan(LaunchAction.Uninstall);
        }

        public bool CanTryAgain { get { return this.State == InstallationState.Failed; } }
        public void TryAgain()
        {
            this.Plan(this.plannedAction);
        }

        #endregion

    }
}
