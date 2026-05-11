using System;
using System.Globalization;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Navigation;
using System.Windows.Threading;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;

namespace StatisticsParser.Vsix.Controls
{
    public partial class AboutDialog : Window
    {
        private const string CopyButtonDefaultText = "Copy Version Info";

        public AboutDialog()
        {
            InitializeComponent();
            VersionText.Text = "Version " + GetDisplayVersion();
        }

        private static string GetDisplayVersion()
        {
            try
            {
                var attr = Assembly.GetExecutingAssembly().GetCustomAttribute<AssemblyFileVersionAttribute>();
                if (attr != null && !string.IsNullOrEmpty(attr.Version))
                    return attr.Version;
            }
            catch { }
            return Vsix.Version;
        }

        private void OnNavigate(object sender, RequestNavigateEventArgs e)
        {
            try
            {
                VsShellUtilities.OpenSystemBrowser(e.Uri.AbsoluteUri);
            }
            catch { }
            e.Handled = true;
        }

        private void OnCopyDiagnostics(object sender, RoutedEventArgs e)
        {
            var text = BuildDiagnostics();
            try
            {
                Clipboard.SetText(text);
                FlashCopiedFeedback();
            }
            catch { }
        }

        private void OnClose(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private string BuildDiagnostics()
        {
            string version = SafeGet(GetDisplayVersion);
            string ssms = SafeGet(GetSsmsVersionLine);
            string dotnet = SafeGet(() => RuntimeInformation.FrameworkDescription);
            string os = SafeGet(() => RuntimeInformation.OSDescription + " (" + RuntimeInformation.OSArchitecture + ")");
            string culture = SafeGet(() => CultureInfo.CurrentCulture.Name);

            var sb = new StringBuilder();
            sb.Append("Statistics Parser: ").AppendLine(version);
            sb.Append("SSMS:    ").AppendLine(ssms);
            sb.Append(".NET:    ").AppendLine(dotnet);
            sb.Append("OS:      ").AppendLine(os);
            sb.Append("Culture: ").AppendLine(culture);
            return sb.ToString();
        }

        private static string GetSsmsVersionLine()
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            var dte = Package.GetGlobalService(typeof(SDTE)) as EnvDTE.DTE;
            if (dte == null) return "(unavailable)";
            return dte.Version + "  (" + dte.Edition + ")";
        }

        private static string SafeGet(Func<string> getter)
        {
            try { return getter() ?? "(null)"; }
            catch (Exception ex) { return "(error: " + ex.GetType().Name + ")"; }
        }

        private void FlashCopiedFeedback()
        {
            CopyButton.Content = "Copied";
            var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(1500) };
            timer.Tick += (s, e) =>
            {
                CopyButton.Content = CopyButtonDefaultText;
                timer.Stop();
            };
            timer.Start();
        }
    }
}
