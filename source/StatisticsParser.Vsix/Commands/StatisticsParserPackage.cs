using System;
using System.Runtime.InteropServices;
using System.Threading;
using Microsoft.VisualStudio.Shell;
using StatisticsParser.Vsix.Windows;
using Task = System.Threading.Tasks.Task;

namespace StatisticsParser.Vsix.Commands
{
    [PackageRegistration(UseManagedResourcesOnly = true, AllowsBackgroundLoading = true)]
    [InstalledProductRegistration("#110", "#112", "1.0.0", IconResourceID = 400)]
    [ProvideMenuResource("Menus.ctmenu", 1)]
    [ProvideToolWindow(typeof(StatisticsParserToolWindow))]
    [Guid(PackageGuidString)]
    public sealed class StatisticsParserPackage : AsyncPackage
    {
        public const string PackageGuidString = "0F240EE5-54A7-43CB-9710-3A8E2DEA5B46";

        protected override async Task InitializeAsync(CancellationToken cancellationToken, IProgress<ServiceProgressData> progress)
        {
            await JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);
            await ParseStatisticsCommand.InitializeAsync(this);
        }
    }
}
