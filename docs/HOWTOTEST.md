# HOWTOTEST.md

How to build, deploy, and launch the Statistics Parser VSIX into the SSMS 22 **experimental hive** (`/RootSuffix Exp`) for testing.

## Prerequisites

- **Windows** with **Visual Studio 2026** (any edition) installed at `C:\Program Files\Microsoft Visual Studio\18\<Edition>\` — needed for `MSBuild.exe` and the VS SDK targets.
- **SSMS 22** installed at `C:\Program Files\Microsoft SQL Server Management Studio 22\Release\` — provides `VSIXInstaller.exe` and `SSMS.exe`.
- All Core tests green: `dotnet test source/StatisticsParser.Core.Tests`.

Adjust the VS edition path below (`Professional` / `Enterprise` / `Community`) to match your install.

## Procedure

Run the steps below in PowerShell. Set `$repo` once at the top so the rest works regardless of your current directory. Each step is required — skipping step 3 in particular will leave SSMS launching against a stale catalog and your changes will not appear.

```powershell
# 0. Kill SSMS processes
Get-Process SSMS -ErrorAction SilentlyContinue | Stop-Process -Force
Start-Sleep -Seconds 2

# 1. Rebuild Release VSIX (already built above, but rerun if you've made changes since).
$repo = "Z:\Brent Ozar Unlimited\Code\StatisticsParserExtension"
& "C:\Program Files\Microsoft Visual Studio\18\Professional\MSBuild\Current\Bin\MSBuild.exe" `
  "$repo\source\StatisticsParser.Vsix\StatisticsParser.Vsix.csproj" `
  /p:Configuration=Debug /p:Platform=x64 /t:Rebuild /v:minimal /nologo

# 2. Uninstall previous version
$installer = "C:\Program Files\Microsoft SQL Server Management Studio 22\Release\Common7\IDE\VSIXInstaller.exe"
& $installer /quiet /rootSuffix:Exp /uninstall:StatisticsParser.4A9EFF2E-819B-453D-BE4C-5DF7B343C0E7

# 3. Install fresh into Exp hive
$vsix = "$repo\source\StatisticsParser.Vsix\bin\x64\Debug\StatisticsParser.vsix"
& $installer /quiet /rootSuffix:Exp /logFile:"$env:TEMP\statsparser-install.log" $vsix

# 4. MANDATORY: rebuild SSMS extension catalog. Skipping this is why the menu doesn't show.
$ssms = "C:\Program Files\Microsoft SQL Server Management Studio 22\Release\Common7\IDE\Ssms.exe"
& $ssms /UpdateConfiguration /RootSuffix Exp

# 5. Refresh cache, run elevated PowerShell:
$prof = Get-ChildItem "$env:LOCALAPPDATA\Microsoft\SSMS" -Directory |
    Where-Object Name -match '^22\.0_[0-9a-f]+(Exp)?$' |
    Sort-Object LastWriteTime -Descending |
    Select-Object -First 1 -ExpandProperty FullName
Write-Host "Clearing caches in $prof"
foreach ($d in 'ComponentModelCache','ImageLibrary','MEFCacheBackup') {
    $p = Join-Path $prof $d
    if (Test-Path $p) { Remove-Item $p -Recurse -Force }
}

$ssms = "C:\Program Files\Microsoft SQL Server Management Studio 22\Release\Common7\IDE\Ssms.exe"
& $ssms /Setup
& $ssms /Setup /RootSuffix Exp

# 6. Launch the experimental SSMS instance.
& $ssms /RootSuffix Exp /log
```

For a Release build, swap `Configuration=Debug` for `Configuration=Release` in step 1 and adjust the `$vsix` path (`bin\x64\Release\...`).

## Verification

After step 4, in the experimental SSMS instance:

1. Open a `.sql` query window.
2. Run a query with `SET STATISTICS IO, TIME ON;` so the Messages tab has parseable output.
3. Right-click in the query body → **Parse Statistics** appears.
4. Click it → **Stats Parser** tool window docks and renders the parsed output.
5. As a fallback, **Tools → Parse Statistics** runs the same command.

`Tools → Extensions and Updates` should list **Statistics Parser** by **Brent Ozar Unlimited** under the installed extensions.

## Gotchas

- **`/UpdateConfiguration` is the easy-to-miss step.** `VSIXInstaller.exe` updates its own caches but does not trigger SSMS's `ExtensionMetadata2.0.mpack` rebuild. Without step 3, SSMS launches with the stale catalog and ignores the new bits even though the install reported success.
- **Use the `&` call operator, not `Start-Process -ArgumentList`.** The latter splits paths on spaces, which breaks the .vsix path.
- **Always pass `/RootSuffix Exp`** to both `VSIXInstaller.exe` (`/rootSuffix:Exp`) and `SSMS.exe`. Mixing hives — installing to Exp but launching the regular hive, or vice versa — is the most common reason for "I installed it but I don't see it."
- **VSIXInstaller logs** land at `%TEMP%\dd_VSIXInstaller_<timestamp>_<id>.log`; the explicit `/logFile:` in step 2 captures the same content at a known path.
- **SSMS activity log** (when launched with `/log`) lands at `%AppData%\Microsoft\SQL Server Management Studio\22.0_*Exp\ActivityLog.xml` — useful when the extension installs cleanly but fails to load.

## Cleanup

To remove the extension entirely from the experimental hive:

```powershell
& "C:\Program Files\Microsoft SQL Server Management Studio 22\Release\Common7\IDE\VSIXInstaller.exe" `
  /quiet /rootSuffix:Exp /uninstall:StatisticsParser.4A9EFF2E-819B-453D-BE4C-5DF7B343C0E7
& "C:\Program Files\Microsoft SQL Server Management Studio 22\Release\Common7\IDE\SSMS.exe" /UpdateConfiguration /RootSuffix Exp
```

To nuke the experimental hive entirely (last-resort reset): close all SSMS instances, then delete `%LocalAppData%\Microsoft\SSMS\22.0_*Exp\` and `%AppData%\Microsoft\SQL Server Management Studio\22.0_*Exp\`. The next launch with `/RootSuffix Exp` recreates a clean hive.
