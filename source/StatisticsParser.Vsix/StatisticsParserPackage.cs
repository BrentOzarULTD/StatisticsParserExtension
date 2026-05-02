using System;
using System.Runtime.InteropServices;
using System.Threading;
using Community.VisualStudio.Toolkit;
using Microsoft.VisualStudio.Shell;
using StatisticsParser.Vsix.Diagnostics;
using StatisticsParser.Vsix.Windows;
using Task = System.Threading.Tasks.Task;

namespace StatisticsParser.Vsix
{
    [PackageRegistration(UseManagedResourcesOnly = true, AllowsBackgroundLoading = true)]
    [InstalledProductRegistration("#110", "#112", "1.0.0", IconResourceID = 400)]
    [ProvideMenuResource("Menus.ctmenu", 1)]
    [ProvideToolWindow(typeof(StatisticsParserToolWindow))]
    [Guid(PackageGuids.guidStatisticsParserPackageString)]
    public sealed class StatisticsParserPackage : ToolkitPackage
    {
        protected override async Task InitializeAsync(CancellationToken cancellationToken, IProgress<ServiceProgressData> progress)
        {
            await this.RegisterCommandsAsync();
            await MenuGuidCapture.InitializeAsync(this);
        }
    }
}
