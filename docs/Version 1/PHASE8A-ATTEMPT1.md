# Phase 8a — Attempt 1 Synopsis

Status: **RESOLVED 2026-05-01** — see [Resolution](#resolution-2026-05-01) below. The probe command was unblocked once the manifest was reverted to the canonical PHASE7-RESEARCH.md spec and `SSMS.exe /UpdateConfiguration /RootSuffix Exp` was run to force a catalog rebuild. The body below remains as the historical record of the dead ends.

Date: 2026-04-29.

---

## Resolution (2026-05-01)

**Two changes unblocked the load:**

1. **Manifest reverted to PHASE7-RESEARCH.md canonical spec.** The Phase 8a churn ([Manifest churn](#manifest-churn-what-we-tried-in-order)) had ended on a four-target manifest (`Microsoft.VisualStudio.Community/Pro/Enterprise [18.0,19.0)` + `Microsoft.VisualStudio.Ssms [22.0,)`) that was never the recommended configuration. Reverting to a single `Microsoft.VisualStudio.Ssms [22.0,]` amd64 target with `CoreEditor [17.0,19.0)` prerequisite — exactly as [PHASE7-RESEARCH.md:33-47](PHASE7-RESEARCH.md) prescribed — caused VSIXInstaller to commit to `VisualStudioExtensionCache` and `PerUserEnabledExtensionsCache`, which it never did against the bad-target installs. (The bad-target installs deposited files but VSIXInstaller silently took a different code path that skipped the catalog commit.)

2. **`SSMS.exe /UpdateConfiguration /RootSuffix Exp` was required after install.** Even with the canonical-manifest install committing to VSIXInstaller's caches, SSMS still launched with stale `ExtensionMetadata2.0.mpack` from 4/29 and never noticed our extension. Running `/UpdateConfiguration` post-install forced the catalog rebuild — `ExtensionMetadata2.0.mpack` updated and embedded our package GUID `0f240ee5`, identity GUID `4A9EFF2E`, install folder name, and publisher. On next SSMS launch, `Tools → Parse Statistics` appeared and clicking it broke at [Commands/ParseStatisticsCommand.cs:16](../source/StatisticsParser.Vsix/Commands/ParseStatisticsCommand.cs).

**Falsified hypotheses:**
- Hypothesis #1 (cache rebuild) was real — `/UpdateConfiguration` was the missing trigger after every reinstall.
- Hypothesis #2 (missing CodeBase / `[ProvideBindingPath]`) was wrong; the deployed pkgdef always had a correct `CodeBase` entry.
- Hypothesis #3 (degraded SSMS install) was incorrect — the install was fine; the unrelated CTO failures noted in ActivityLog were red herrings.
- Hypothesis #5 (wrong install-target ID) was partly correct — the four-target manifest was the actual blocker, not the choice of `Microsoft.VisualStudio.Ssms` per se. The single-target spec from PHASE7-RESEARCH.md was right all along.

**Still open / deferred:**
- Right-click placement on `IDM_VS_CTXT_CODEWIN` does not surface in SSMS 22's query window (which is hosted by `SqlScriptEditorControl`, not a standard VS code window). [PHASE7-RESEARCH.md:122-124](PHASE7-RESEARCH.md) already deferred this to empirical-hive discovery in Phase 8a; that work can now actually run via the Probe Messages Source command. Tools-menu placement works fine.
- The Phase 8a probe code (`Diagnostics/`) is now reachable; running it is the next step to settle the Messages-text accessor question for Phase 8b.

**Reproducible deploy procedure (going forward):**
```powershell
# 1. Build (msbuild from VS 2026; dotnet build alone does NOT produce the .vsix)
& "C:\Program Files\Microsoft Visual Studio\18\Professional\MSBuild\Current\Bin\MSBuild.exe" `
  "source\StatisticsParser.Vsix\StatisticsParser.Vsix.csproj" `
  /p:Configuration=Debug /p:Platform=x64 /t:Rebuild /v:minimal /nologo

# 2. Uninstall + reinstall (use call operator, not Start-Process -ArgumentList — the latter splits paths on spaces)
$installer = "C:\Program Files\Microsoft SQL Server Management Studio 22\Release\Common7\IDE\VSIXInstaller.exe"
$vsix = "Z:\Brent Ozar Unlimited\Code\StatisticsParserExtension\source\StatisticsParser.Vsix\bin\x64\Debug\net48\StatisticsParser.Vsix.vsix"
& $installer /quiet /rootSuffix:Exp /uninstall:StatisticsParser.4A9EFF2E-819B-453D-BE4C-5DF7B343C0E7
& $installer /quiet /rootSuffix:Exp /logFile:"$env:TEMP\statsparser-install.log" $vsix

# 3. Force catalog rebuild — required after every reinstall
& "C:\Program Files\Microsoft SQL Server Management Studio 22\Release\Common7\IDE\SSMS.exe" /UpdateConfiguration /RootSuffix Exp

# 4. Launch
& "C:\Program Files\Microsoft SQL Server Management Studio 22\Release\Common7\IDE\SSMS.exe" /RootSuffix Exp /log
```

---

## What we built

All in `source/StatisticsParser.Vsix/Diagnostics/` (throwaway diagnostic code; remove when Phase 8b lands):

| File | Role |
|---|---|
| `ProbeOutputPane.cs` | Wrapper around a dedicated VS Output pane named "Statistics Parser — Probe". `WriteHeader / WriteSuccess / WriteFailure / WriteInfo`. Pane GUID `F1E27B41-1A05-4D89-9E6F-F1E27B411A05`. |
| `BrokeredContractProbe.cs` | Option 1 — loads `Microsoft.SqlServer.Management.UI.VSIntegration.SqlEditor.BrokeredContracts.dll` from `AppDomain.BaseDirectory`, lists matching interfaces, finds `ServiceMoniker` static members, attempts to acquire `IServiceBroker` reflectively via `SVsBrokeredServiceContainer`, probes each (moniker, paired-interface) pair with `GetProxyAsync<T>`. |
| `TextBufferProbe.cs` | Option 2 — walks `IVsRunningDocumentTable`, dumps line previews of every `IVsTextLines` buffer; also walks WPF visual tree under all `Application.Current.Windows` for elements named `Message/Result/SqlScript/EditorControl` and dumps DataContext properties matching `Message/Text`. |
| `ReflectionProbe.cs` | Option 3 — locates `SqlScriptEditorControl`-like instances in the visual tree, dumps every public + non-public field/property whose name matches `Message/Result/Pane/Output/Text`, walking the full base-type chain. |
| `QueryEventsProbe.cs` | Option 4 — enumerates event-shaped types in the brokered-contracts DLL (filter `Event/Notification/Listener/Completed/Subscriber/Observer/BatchExecut/QueryExecut`). Logs only — does not subscribe. |
| `MenuGuidCapture.cs` | Passive `IOleCommandTarget` registered ahead of the editor command target via `IVsRegisterPriorityCommandTarget`. Dedupe-counts every `(cmdGroup, cmdId)` seen via `QueryStatus`. Dumped + reset by the orchestrator. |
| `MessageSourceProbeCommand.cs` | Orchestrator. Bound to `cmdidProbeMessageSource = 0x0101`. Runs all five probes in order, writes a structured report to `ProbeOutputPane`, then opens the tool window with a "probe ran" message. |

Modifications to existing files:

| File | Change |
|---|---|
| [StatisticsParser.vsct](../source/StatisticsParser.Vsix/StatisticsParser.vsct) | Added `cmdidProbeMessageSource = 0x0101` button beside Parse Statistics in the same `ParseStatisticsGroup`; added Tools-menu placement. |
| [StatisticsParserPackage.cs](../source/StatisticsParser.Vsix/Commands/StatisticsParserPackage.cs) | `InitializeAsync` now also calls `MessageSourceProbeCommand.InitializeAsync` and `MenuGuidCapture.InitializeAsync`. |
| [StatisticsParserControl.xaml.cs](../source/StatisticsParser.Vsix/Controls/StatisticsParserControl.xaml.cs) | Added `ShowProbeRanMessage(outputPaneTitle)` to update the placeholder TextBlock. |
| [source.extension.vsixmanifest](../source/StatisticsParser.Vsix/source.extension.vsixmanifest) | See "manifest churn" below — settled at version `1.0.3`, install targets both `Microsoft.VisualStudio.Community [18.0,19.0)` amd64 AND `Microsoft.VisualStudio.Ssms [18.0,)` amd64. |

Deleted: `source/StatisticsParser.Vsix/extension.vsixmanifest` (stale duplicate at project root — see "Stale manifest" below).

Build status: `dotnet test source/StatisticsParser.Core.Tests` — all 71 tests pass. `msbuild source/StatisticsParser.Vsix /p:Configuration=Debug /p:Platform=x64` — succeeds, produces `bin/x64/Debug/net48/StatisticsParser.Vsix.vsix` (~112 KB).

---

## Build issues we hit and fixed

| Issue | Root cause | Fix |
|---|---|---|
| `error CS0246: IVsTextLines` | Wrong namespace | Added `using Microsoft.VisualStudio.TextManager.Interop` to `TextBufferProbe.cs`. |
| `error CS0104: 'Window' is ambiguous` | `EnvDTE.Window` vs `System.Windows.Window` collision in `TextBufferProbe.WalkVisualTree` | Fully qualified as `System.Windows.Window`. |
| Multiple `VSTHRD010` warnings about main-thread access | Threading analyzer can't prove the probe stays on the UI thread | Left as warnings (build succeeded). Worth re-evaluating if Phase 8a is ever productionized. |
| **Stale `extension.vsixmanifest` at project root** | A non-standard duplicate file existed alongside the proper `source.extension.vsixmanifest`. The VSSDK build was packaging the stale one, so version bumps and command additions never made it into the .vsix. | Deleted the stray file. The build now correctly generates `bin/.../extension.vsixmanifest` from `source.extension.vsixmanifest`. |
| `VSSDK1311: must contain a value for 'PackageManifest:Prerequisites'` | Tried removing prerequisites entirely | Schema requires at least one. Kept `Microsoft.VisualStudio.Component.CoreEditor [17.0,19.0)`. |

---

## Manifest churn (what we tried, in order)

| Version | Install Target(s) | Outcome |
|---|---|---|
| `1.0.0` | `Microsoft.VisualStudio.Ssms [22.0,)` amd64 (per PHASE7-RESEARCH) | VSIXInstaller reports success. Extension does NOT appear in Tools → Extensions. Package GUID never written to `privateregistry.bin` by `/UpdateConfiguration`. |
| `1.0.1` | Same | Bump intended to defeat menu cache. Same outcome. **Discovered** at this point that the project-root `extension.vsixmanifest` was stale — version bumps weren't even packaged until we deleted it. |
| `1.0.2` | `Microsoft.VisualStudio.Ssms [18.0,)` amd64 | Switched the version range to match Microsoft's bundled `SSMS.SqlCompletions` manifest. Same outcome. |
| `1.0.3` (current) | `Microsoft.VisualStudio.Community [18.0,19.0)` amd64 AND `Microsoft.VisualStudio.Ssms [18.0,)` amd64 | Switched primary install-target ID to `Microsoft.VisualStudio.Community` (which is what bundled Copilot, DebugAdapterHost, DebuggerServices use). Same outcome. |

Prerequisites stayed at `Microsoft.VisualStudio.Component.CoreEditor [17.0,19.0)` throughout (matches brink-daniel and Microsoft's bundled Copilot manifest).

---

## Symptoms after each install

- VSIXInstaller log (`%TEMP%\dd_VSIXInstaller_*.log`) **always reports success**, deposits files at e.g. `C:\Users\richie\AppData\Local\Microsoft\SSMS\22.0_e4014512\Extensions\<random>\` (regular hive when `/rootSuffix:Exp` not passed).
- Files on disk are correct: `StatisticsParser.Vsix.dll`, `StatisticsParser.Core.dll`, `StatisticsParser.Vsix.pkgdef`, `extension.vsixmanifest`, `manifest.json`, `catalog.json`.
- `extension.vsixmanifest` content matches what we built.
- `StatisticsParser.Vsix.pkgdef` content looks syntactically correct (Packages key, Menus key, ToolWindows key).
- `StatisticsParser.Vsix.dll` embedded resources include `VSPackage.resources` containing a 719-byte `Menus.ctmenu` byte array (verified by reflection).
- After `/UpdateConfiguration` rebuilds `privateregistry.bin`, the package GUID `0f240ee5` appears **0 times** (Unicode AND ASCII scan).
- `Tools → Extensions` in launched SSMS does NOT list "Statistics Parser".
- Right-click in editor does NOT show **Parse Statistics** OR **Probe Messages Source**.
- ActivityLog.xml entries filtered by our package GUID or "StatisticsParser" return **nothing**. SSMS apparently never even tries to load the package.

ActivityLog DOES show many `HrLoadNativeUILibrary failed with 0x800a006f` / `Failed to find CTMENU resource '#1000'` errors for **other** VS-shipped packages (D549BC66, BEB01DDF, ADCCE324, etc.) — possibly indicating the SSMS install itself is in a partly-degraded state, but those are unrelated GUIDs to ours.

---

## Critical late-stage discovery (last thing we tested)

User's final uninstall verification turned up a leftover install at `C:\Users\richie\AppData\Local\Microsoft\SSMS\22.0_e4014512Exp\Extensions\bb5zskcq.i5s\StatisticsParser.Vsix.pkgdef` — the **experimental hive**. This means at least one earlier `VSIXInstaller.exe /rootSuffix:Exp` actually succeeded into the Exp hive — but throughout the session the user was launching SSMS WITHOUT `/RootSuffix Exp`, so they were checking the wrong hive.

**This was never tested to its conclusion.** It is plausible the extension was loading correctly in the Exp hive the entire time, and we just never opened that instance to verify. Next session should start by:

1. Reinstalling into the Exp hive: `VSIXInstaller.exe /rootSuffix:Exp <path>`
2. Launching the matching hive: `SSMS.exe /RootSuffix Exp`
3. Checking Tools → Extensions in THAT instance.

If commands are present in Exp hive — Phase 8a deployment was correct all along; resume with running the probe.
If commands are still absent — pursue the open hypotheses below.

---

## Open hypotheses (untested when session ended)

1. **Per-user extension discovery uses a different mechanism than `/UpdateConfiguration`.** SSMS's startup may scan `extensions.<lang>.cache` (which is missing in this user's regular hive — only `extensions.configurationchanged` 0-byte sentinel exists), build the extension list dynamically, and merge per-user .pkgdefs at first-launch. `/UpdateConfiguration` may only register system-level extensions. Verifying by checking `privateregistry.bin` may have been the wrong probe entirely. Re-test by launching SSMS normally and checking Tools → Extensions, not the bin file.

2. **`.pkgdef` is missing a `CodeBase` entry.** Compared to bundled `CodeSenseFramework.pkgdef`, ours has no `CodeBase` entry pointing to `$PackageFolder$\StatisticsParser.Vsix.dll`. Without a CodeBase, the CLR loader has no path to find the assembly, so package class resolution may silently fail. Adding `[$RootKey$\BindingPaths\{0f240ee5-...}]` `"$PackageFolder$"=""` may be required.

3. **The SSMS install itself is in a degraded state.** ActivityLog shows many CTO load failures for unrelated VS-shipped packages. A repair install of SSMS 22 may unblock things.

4. **`Microsoft.VisualStudio.Component.CoreEditor` prerequisite isn't satisfied in this user's SSMS 22 install.** Microsoft's bundled extensions still list it — but those are special-cased as `SystemComponent="true"`. A user-installed extension with this prerequisite may be silently filtered if the component isn't registered. Test by removing the prerequisite (will require some other dummy prerequisite to satisfy `VSSDK1311`).

5. **The `Microsoft.VisualStudio.Community` install-target ID is wrong** because we are running SSMS 22, not VS 2026 Community. PHASE7-RESEARCH was right that `Microsoft.VisualStudio.Ssms` is the SSMS-specific ID. The "Community" ID may install successfully but be runtime-rejected. Try reverting to `Microsoft.VisualStudio.Ssms [18.0,)` only.

---

## Environment captured

- SSMS 22 install: `C:\Program Files\Microsoft SQL Server Management Studio 22\Release\Common7\IDE\` — version 22.5.11723.231 (`built by: stable`), shell 18.0.x.
- SSMS 21 also installed: `C:\Program Files\Microsoft SQL Server Management Studio 21\Release\Common7\IDE\` — version 21.6.36603.0 (`built by: d17.14`). Receives our extension when install-target version range allows it.
- VS 2026 Pro: `C:\Program Files\Microsoft Visual Studio\18\Professional\`. MSBuild used for command-line builds.
- Regular hive (SSMS 22): `C:\Users\richie\AppData\Local\Microsoft\SSMS\22.0_e4014512\`.
- Exp hive (SSMS 22): `C:\Users\richie\AppData\Local\Microsoft\SSMS\22.0_e4014512Exp\`.
- ActivityLog: `C:\Users\richie\AppData\Roaming\Microsoft\SSMS\22.0_e4014512\ActivityLog.xml` (only written when SSMS launched with `/log`).
- VSIXInstaller logs: `%TEMP%\dd_VSIXInstaller_<timestamp>_<id>.log`.

---

## Useful one-liners (preserved)

Build:
```powershell
& "C:\Program Files\Microsoft Visual Studio\18\Professional\MSBuild\Current\Bin\MSBuild.exe" "source\StatisticsParser.Vsix\StatisticsParser.Vsix.csproj" /p:Configuration=Debug /p:Platform=x64 /t:Rebuild /v:minimal /nologo
```

Install (regular hive):
```powershell
& "C:\Program Files\Microsoft SQL Server Management Studio 22\Release\Common7\IDE\VSIXInstaller.exe" "Z:\Brent Ozar Unlimited\Code\StatisticsParserExtension\source\StatisticsParser.Vsix\bin\x64\Debug\net48\StatisticsParser.Vsix.vsix"
```

Install (Exp hive):
```powershell
& "C:\Program Files\Microsoft SQL Server Management Studio 22\Release\Common7\IDE\VSIXInstaller.exe" /rootSuffix:Exp "Z:\Brent Ozar Unlimited\Code\StatisticsParserExtension\source\StatisticsParser.Vsix\bin\x64\Debug\net48\StatisticsParser.Vsix.vsix"
```

Uninstall (regular):
```powershell
& "C:\Program Files\Microsoft SQL Server Management Studio 22\Release\Common7\IDE\VSIXInstaller.exe" /uninstall:StatisticsParser.4A9EFF2E-819B-453D-BE4C-5DF7B343C0E7
```

Uninstall (Exp):
```powershell
& "C:\Program Files\Microsoft SQL Server Management Studio 22\Release\Common7\IDE\VSIXInstaller.exe" /rootSuffix:Exp /uninstall:StatisticsParser.4A9EFF2E-819B-453D-BE4C-5DF7B343C0E7
```

Verify removal across all hives:
```powershell
Get-ChildItem "$env:LocalAppData\Microsoft\SSMS\*\Extensions" -Recurse -Filter "StatisticsParser.Vsix.pkgdef" -ErrorAction SilentlyContinue | Select-Object FullName
```

Inspect built VSIX manifest:
```powershell
$vsix = "Z:\Brent Ozar Unlimited\Code\StatisticsParserExtension\source\StatisticsParser.Vsix\bin\x64\Debug\net48\StatisticsParser.Vsix.vsix"; Add-Type -AssemblyName System.IO.Compression.FileSystem; $z = [System.IO.Compression.ZipFile]::OpenRead($vsix); $sr = New-Object System.IO.StreamReader(($z.Entries | Where-Object Name -eq "extension.vsixmanifest").Open()); $sr.ReadToEnd(); $sr.Close(); $z.Dispose()
```

Search privateregistry.bin for our package (run only when SSMS is closed):
```powershell
$bytes = [System.IO.File]::ReadAllBytes("C:\Users\richie\AppData\Local\Microsoft\SSMS\22.0_e4014512\privateregistry.bin"); ([regex]::Matches([System.Text.Encoding]::Unicode.GetString($bytes), "0f240ee5", "IgnoreCase")).Count
```

Dump our embedded `Menus.ctmenu` resource to confirm it's there:
```powershell
$dll = "Z:\Brent Ozar Unlimited\Code\StatisticsParserExtension\source\StatisticsParser.Vsix\bin\x64\Debug\net48\StatisticsParser.Vsix.dll"; $asm = [System.Reflection.Assembly]::LoadFile($dll); $rs = $asm.GetManifestResourceStream("VSPackage.resources"); $rr = New-Object System.Resources.ResourceReader($rs); foreach ($e in $rr) { Write-Host "  key=$($e.Key) size=$(if ($e.Value -is [byte[]]) { $e.Value.Length } else { 'n/a' })" }
```

---

## Resume checklist for next session

1. **Verify whether the Exp hive install worked all along.** Reinstall to Exp, launch with `/RootSuffix Exp`, check Tools → Extensions and right-click menu. (5 minutes — could short-circuit everything else.)
2. If Exp hive also fails: add `[$RootKey$\BindingPaths\{0f240ee5-...}]` `"$PackageFolder$"=""` to the .pkgdef (via `[ProvideBindingPath]` attribute on the package class) and rebuild.
3. If still failing: revert install target to `Microsoft.VisualStudio.Ssms [18.0,)` only (drop Community).
4. If still failing: try removing the `CoreEditor` prerequisite by switching to a different prerequisite that's known-registered in the user's SSMS install.
5. If still failing: consider a repair install of SSMS 22 — the activity log already shows many CTO failures for unrelated VS-shipped packages.
