# Phase 7 Research Spikes

Resolves the two "Discovery task" rows in [PLAN.md §Design Decisions](PLAN.md) (#4 and #5). Date of investigation: 2026-04-29. SSMS build inspected: **22.0.11205.157** (`built by: d18.0`), shell assembly version `18.0.42373.18589`.

| Spike | Status | One-line outcome |
|---|---|---|
| 1 — SDK NuGet packages | ✅ Resolved | Use `Microsoft.VisualStudio.SDK` 17.14.40265 + `Microsoft.VSSDK.BuildTools` 17.14.2120; target SSMS via `Microsoft.VisualStudio.Ssms [22.0,]` |
| 2 — Messages tab context menu / pane GUID | ⚠️ **Architectural finding — Phase 8 needs adjustment** | Messages tab in SSMS 22 is **not** an `IVsOutputWindow` pane; defer GUID identification to Phase 7 prototyping in the experimental hive |

---

## Spike 1 — SSMS 22 SDK NuGet packages

### Recommended `<PackageReference>` block

Drop this into [StatisticsParser.Vsix.csproj](../source/StatisticsParser.Vsix/StatisticsParser.Vsix.csproj) when Phase 7 begins:

```xml
<ItemGroup>
  <PackageReference Include="Microsoft.VisualStudio.SDK" Version="17.14.40265">
    <ExcludeAssets>runtime</ExcludeAssets>
  </PackageReference>
  <PackageReference Include="Microsoft.VSSDK.BuildTools" Version="17.14.2120">
    <PrivateAssets>all</PrivateAssets>
  </PackageReference>
</ItemGroup>
```

Plus the existing `<UseWPF>true</UseWPF>` and `<TargetFramework>net48</TargetFramework>` already in the csproj. `CreateVsixContainer` and the `Microsoft.VsSDK.targets` import are added by `Microsoft.VSSDK.BuildTools` automatically — no manual import needed in an SDK-style csproj.

### Recommended `source.extension.vsixmanifest` install targeting

```xml
<Installation>
  <InstallationTarget Id="Microsoft.VisualStudio.Ssms" Version="[22.0,]">
    <ProductArchitecture>amd64</ProductArchitecture>
  </InstallationTarget>
</Installation>
<Dependencies>
  <Dependency Id="Microsoft.Framework.NDP" DisplayName="Microsoft .NET Framework" Version="[4.5,)" />
</Dependencies>
<Prerequisites>
  <Prerequisite Id="Microsoft.VisualStudio.Component.CoreEditor"
                Version="[17.0,19.0)"
                DisplayName="Visual Studio core editor" />
</Prerequisites>
```

### Per-package rationale

| Package | Version | Why this version | Citation |
|---|---|---|---|
| `Microsoft.VisualStudio.SDK` | `17.14.40265` | Latest **stable** of the 17.x metapackage line (5/14/2025). Microsoft has not yet published a v18 metapackage for VS 2026 — only the 17.x line is available. The Microsoft "Upgrade a Visual Studio extension" doc explicitly says VS 2022 (17.x) extensions install on VS 2026 with minimal breaking changes. | [nuget.org/packages/Microsoft.VisualStudio.SDK](https://www.nuget.org/packages/Microsoft.VisualStudio.SDK/), [Microsoft Q&A — when will v18 ship](https://learn.microsoft.com/en-us/answers/questions/5618866/when-will-microsoft-visualstudio-interop-v18-and-r), [Upgrade a VS extension](https://learn.microsoft.com/en-us/visualstudio/extensibility/migration/update-visual-studio-extension?view=visualstudio) |
| `Microsoft.VSSDK.BuildTools` | `17.14.2120` | Latest stable 17.x build tools; this is what working third-party SSMS 22 extensions actually ship with (brink-daniel's `ssms-object-explorer-menu` pins `17.14.2094`). An 18.5.40034 was published 4/16/2026 but is brand-new and untested by any third-party SSMS 22 extension we located — defer adoption until 17.x has a known incompatibility. | [nuget.org/packages/Microsoft.VSSDK.BuildTools](https://www.nuget.org/packages/Microsoft.VSSDK.BuildTools/), [brink-daniel/ssms-object-explorer-menu csproj](https://github.com/brink-daniel/ssms-object-explorer-menu/blob/main/SSMSObjectExplorerMenu.csproj) |
| `Microsoft.VisualStudio.Ssms` install target | `[22.0,]` | Verified pattern in two independent sources: brink-daniel's working SSMS 22 extension manifest, and Microsoft's bundled `Extensions\Microsoft\SSMS.SqlCompletions\extension.vsixmanifest` (which uses the same Id with `[18.0,)` for shell-version targeting). For a third-party VSIX targeting SSMS users, the SSMS product version `[22.0,]` is the more semantically accurate of the two. | [SSMS.SqlCompletions vsixmanifest in install dir](#installed-bundled-manifests-spike-1-evidence), [brink-daniel manifest](https://github.com/brink-daniel/ssms-object-explorer-menu/blob/main/source.extension.vsixmanifest) |
| `Microsoft.VisualStudio.Component.CoreEditor` prerequisite | `[17.0,19.0)` | Mirrors brink-daniel's working SSMS 22 manifest. Upper bound `19.0` allows VS 18 (the SSMS 22 shell) without locking forward. Microsoft's bundled `SSMSAgent` manifest uses `[18.0,)` but that targets the underlying VS 2026 Community build; we want the same range that has been proven to install on SSMS 22 for an external extension. | [brink-daniel manifest](https://github.com/brink-daniel/ssms-object-explorer-menu/blob/main/source.extension.vsixmanifest) |

### Notes on stable/prerelease and version-pin risk

- **No v18 SDK metapackage exists on NuGet** as of 2026-04-29. Microsoft Q&A: *"Currently, Microsoft has not released version 18 of the Microsoft.VisualStudio.Interop and related interop packages on NuGet… There is no official preview or prerelease feed for these packages."* When v18 ships, this csproj should be updated; until then, 17.14.x is the only path that compiles.
- **VS 2026 backward compatibility is officially supported.** Microsoft Learn: *"With Visual Studio 2026, users can easily install your Visual Studio 2022 extensions. Since there are minimal breaking changes, upgrading your extension should be straightforward."* The same applies to SSMS 22, which is built on the VS 2026 shell.
- **`net48` is fine.** The 17.14.x SDK metapackage targets `net472`+; `net48` is a strict superset. PLAN.md's `net48` target stands.
- **`x64` platform is required.** Per the Microsoft upgrade doc, *"Even if you don't reference any breaking changes, extensions must be compiled with the Any CPU or x64 platform. The x86 platform is incompatible with the 64-bit process in Visual Studio 2022."* The csproj already pins `<Platforms>x64</Platforms>`; the manifest must declare `<ProductArchitecture>amd64</ProductArchitecture>`.

### Local install evidence (Spike 1)

Captured via PowerShell against `C:\Program Files\Microsoft SQL Server Management Studio 22\Release\`:

| File | Version | Significance |
|---|---|---|
| `Common7\IDE\SSMS.exe` | `22.0.11205.157` (built by `d18.0`) | Confirms SSMS 22 GA, shell-major 18 |
| `Common7\IDE\Microsoft.VisualStudio.Shell.Styles.dll` | `18.0.42373.18589` | Shell version pin |
| `Common7\IDE\Microsoft.VisualStudio.Shell.UI.Internal.dll` | `18.0.42373.18589` | Shell version pin |
| `Common7\IDE\Microsoft.VisualStudio.Shell.ViewManager.dll` | `18.0.42373.18589` | Shell version pin |

#### Installed bundled manifests (Spike 1 evidence)

Three Microsoft-bundled extensions in `Common7\IDE\Extensions\Microsoft\` show the install-target patterns Microsoft itself uses inside SSMS 22:

```xml
<!-- SSMS.SqlCompletions/extension.vsixmanifest (SystemComponent) -->
<InstallationTarget Id="Microsoft.VisualStudio.Ssms" Version="[18.0,)" />

<!-- SSMSAgent/extension.vsixmanifest (out-of-proc, dotnet 8) -->
<InstallationTarget Id="Microsoft.VisualStudio.Community" Version="[18.0,19.0)">
  <ProductArchitecture>amd64</ProductArchitecture>
</InstallationTarget>

<!-- SSMS.PresenterMode/extension.vsixmanifest (legacy, pre-GA) -->
<InstallationTarget Id="Microsoft.VisualStudio.Community" Version="[17.9, 18.0)">
  <ProductArchitecture>amd64</ProductArchitecture>
</InstallationTarget>
```

Conclusion: `Id="Microsoft.VisualStudio.Ssms"` is the SSMS-specific install target. The SSMS product-version range `[22.0,]` (used by brink-daniel) is interchangeable with the shell-version range `[18.0,)` (used by SqlCompletions); the SSMS product-version is more readable for a third-party extension.

---

## Spike 2 — Messages tab context menu GUID + pane GUID

### Status: ⚠️ Inconclusive — defer to Phase 7 prototyping

Strong architectural finding: **The Messages tab in the SSMS 22 query window is not an `IVsOutputWindow` pane.** This invalidates the assumption baked into [PLAN.md Phase 8](PLAN.md) that calling `IVsOutputWindow.GetPane()` with a "Messages pane GUID" will retrieve the parsed text. Phase 8 needs replanning before the `MessagesTabReader` can be implemented.

### Evidence (local SSMS 22 install)

| Observation | What it tells us |
|---|---|
| `Get-ChildItem … -Filter '*.vsct'` returns **0 files** in the entire SSMS install | All command tables are compiled into binary resources inside DLLs (`SQLEditors.dll`, etc.). No text VSCT to grep for menu IDs. |
| `Get-ChildItem … -Filter '*.ctc'` returns **0 files** | Same — even the older `.ctc` form isn't shipped on disk. |
| 254 `.pkgdef` files exist; full-text search for `Messages` finds **zero** in any SSMS-specific package definition | The Messages tab is not registered as a named Output pane. |
| Only `OutputWindow\{FC076020-078A-11D1-A7DF-00A0C9110051}` is registered (in `SQLEditors.pkgdef` and `VSDebug.pkgdef`) | This is the SQL Server **Tools Output** pane (a sibling of "Build" / "Debug" in the *Output* window). It is **not** the per-query Messages tab. |
| `Microsoft.SqlServer.Management.UI.VSIntegration.dll` (the classic SSMS shell-integration assembly with `VsMenus`/`VSStandardCommands97` constants) is **absent** from SSMS 22 | The classic API surface used by SSMS 18/19 extensions is gone. |
| Only `Microsoft.SqlServer.Management.UI.VSIntegration.SqlEditor.BrokeredContracts.dll` (v22.0.103.0) ships in the IDE root | New brokered-services architecture. SSMS 22 exposes editor/query services via brokered contracts, not direct interop assemblies. |
| `SQLEditors.dll` (v22.0.103.0) at `Common7\IDE\Extensions\Application\` registers `EditorFactoryPackage`, `VirtualProject`, and `SqlScriptEditorControl` | The query window (with its embedded Messages tab) is owned by this package; menu IDs and the Messages pane access live inside compiled resources of this DLL. |
| `Microsoft.SqlServer.GridControl.dll` and `Microsoft.SqlServer.DlgGrid.dll` (v22.0.103.0) | Host the result/messages tabbed UI; not VS shell controls. |

### What this means for Phase 7 / Phase 8

**For the menu placement (originally "Spike 2 deliverable A — Messages tab context menu GUID"):**

- The Messages-tab-specific context menu **cannot be discovered from documentation or static install inspection** — it's only accessible via the compiled resources in `SQLEditors.dll` or by attaching a debugger to a running SSMS instance and probing the right-click menu chain.
- **Recommendation for Phase 7:** ship a first iteration that places "Parse Statistics" on the standard text-editor context menu (`IDM_VS_CTXT_CODEWIN` under `guidSHLMainMenu`, both publicly documented in [VsMenus](https://learn.microsoft.com/en-us/dotnet/api/microsoft.visualstudio.shell.vsmenus.idm_vs_ctxt_codewin?view=visualstudiosdk-2022)) plus a Tools menu entry. This makes the feature reachable while we discover the Messages-tab GUID empirically in the experimental hive.
- The discovery path itself is a Phase 7 sub-task, not a research spike: load a stub VSIX into the experimental hive, attach a debugger to the right-click chain on the Messages tab, and dump the parent menu's `Guid`/`ID` from `IOleCommandTarget` callbacks.

**For the pane-content read (originally "Spike 2 deliverable B — Messages pane GUID for `IVsOutputWindow.GetPane()`"):**

- The Messages tab is **not** an `IVsOutputWindow` pane. The PLAN.md Phase 8 algorithm (`GetService(SVsOutputWindow)` → `GetPane(messagesGuid)` → `GetText()`) will not work.
- Phase 8 needs to be rewritten around one of these alternatives, in roughly increasing order of effort:
  1. **Brokered services**: investigate whether `Microsoft.SqlServer.Management.UI.VSIntegration.SqlEditor.BrokeredContracts` exposes a `GetMessages()` / `GetResultsText()` contract. This is the modern SSMS 22 surface and is the right first probe.
  2. **DTE / `ActiveDocument.Selection`** style automation against the Messages tab's underlying text buffer (if the Messages tab is implemented atop a `IVsTextBuffer` we can locate via the active SqlScriptEditorControl).
  3. **Reflection** into `SQLEditors.dll` — `SqlScriptEditorControl` likely exposes an internal `MessagesText` property. This is fragile (private API; breaks across SSMS versions) but is the established fallback used by older SSMS extensions.
  4. **Query-execution event subscription** — already named as the fallback in [PLAN.md Phase 8 risk table](PLAN.md). Hook into the SqlEditor brokered service's "query completed" event and accumulate the text we receive.
- All four options require an experimental-hive prototype to validate; none can be settled by static research. **Phase 8 should be split into Phase 8a (discover the Messages-text accessor in the experimental hive) and Phase 8b (implement `MessagesTabReader` against the discovered API).**

### Citations

- [VsMenus.IDM_VS_CTXT_CODEWIN field — Microsoft.VisualStudio.Shell](https://learn.microsoft.com/en-us/dotnet/api/microsoft.visualstudio.shell.vsmenus.idm_vs_ctxt_codewin?view=visualstudiosdk-2022) — documents `IDM_VS_CTXT_CODEWIN` as the standard editor right-click menu (the safe Phase-7 fallback placement).
- [VSStandardCommands97.IDM_VS_CTXT_CODEWIN field — Microsoft.SqlServer.Management.UI.VSIntegration](https://learn.microsoft.com/en-us/dotnet/api/microsoft.sqlserver.management.ui.vsintegration.vsstandardcommands97.idm_vs_ctxt_codewin?view=sqlserver-2016) — confirms SSMS reuses the standard VS context menu IDs for code-window placement.
- [Adding a new command to the Output Window's context menu — Microsoft Q&A](https://learn.microsoft.com/en-sg/answers/questions/2247847/adding-a-new-command-to-the-output-windows-context) — confirms the standard Output Window context menu has its own ID set; none of these correspond to the SSMS Messages tab.
- [Extending the Output Window — visualstudio-docs](https://github.com/MicrosoftDocs/visualstudio-docs/blob/main/docs/extensibility/extending-the-output-window.md) — canonical reference for `IVsOutputWindow.GetPane()` usage, against which the SSMS-22 Messages tab is **not** addressable.
- [SSMS Object Explorer Menu — brink-daniel/ssms-object-explorer-menu](https://github.com/brink-daniel/ssms-object-explorer-menu) — working SSMS 22 third-party extension; uses `IDM_VS_CTXT_CODEWIN`-style placement on Object Explorer (not Messages), confirming the empirical-discovery model for non-standard placements.

---

## Recommended PLAN.md follow-ups (after this doc is approved)

1. Replace [PLAN.md §Design Decisions row #4](PLAN.md) text with: "Use `Microsoft.VisualStudio.SDK` 17.14.40265 + `Microsoft.VSSDK.BuildTools` 17.14.2120 + install target `Microsoft.VisualStudio.Ssms [22.0,]` amd64. See [PHASE7-RESEARCH.md](PHASE7-RESEARCH.md)."
2. Replace row #5 text with: "Deferred — Messages tab is not an `IVsOutputWindow` pane in SSMS 22. Initial placement: `IDM_VS_CTXT_CODEWIN`. Final placement discovered in Phase 7 experimental-hive prototype. See [PHASE7-RESEARCH.md](PHASE7-RESEARCH.md)."
3. Rewrite [PLAN.md §Phase 8](PLAN.md) to drop the `IVsOutputWindow.GetPane()` path. Replace the algorithm with: (a) Phase 8a — empirical discovery of the Messages-text accessor (brokered contract, reflection, or completion-event hook) in the experimental hive; (b) Phase 8b — implement `MessagesTabReader` against whichever surface 8a settles on.
4. Update [PLAN.md §Remaining Risks](PLAN.md) to elevate the third row ("`IVsOutputWindowPane.GetText()` may be unavailable") from "may be" to "is — confirmed by SSMS 22 install inspection on 2026-04-29".

These follow-ups are scoped for a separate, small turn after the user reviews this research doc.

---

## Verification

To sanity-check this research yourself:

```powershell
# Spike 1 — confirm shell version against the recommended pin
Get-Item "${env:ProgramFiles}\Microsoft SQL Server Management Studio 22\Release\Common7\IDE\Microsoft.VisualStudio.Shell.Styles.dll" |
  Select-Object -ExpandProperty VersionInfo

# Spike 1 — read the bundled SqlCompletions manifest
Get-Content "${env:ProgramFiles}\Microsoft SQL Server Management Studio 22\Release\Common7\IDE\Extensions\Microsoft\SSMS.SqlCompletions\extension.vsixmanifest"

# Spike 2 — confirm zero text VSCT/CTC files ship in SSMS 22
Get-ChildItem "${env:ProgramFiles}\Microsoft SQL Server Management Studio 22\Release\Common7\IDE" -Recurse -Include '*.vsct','*.ctc' -ErrorAction SilentlyContinue | Measure-Object

# Spike 2 — confirm the classic VSIntegration.dll is absent
Get-ChildItem "${env:ProgramFiles}\Microsoft SQL Server Management Studio 22\Release\Common7\IDE" -Recurse -Filter 'Microsoft.SqlServer.Management.UI.VSIntegration*.dll' |
  Select-Object Name, @{n='V';e={$_.VersionInfo.FileVersion}}
```

Expected: shell version `18.0.x`, SqlCompletions manifest contains `Microsoft.VisualStudio.Ssms`, zero VSCT/CTC files, only the `BrokeredContracts` variant of the VSIntegration assembly.

Visit one of the cited URLs (e.g., [Microsoft.VisualStudio.SDK on NuGet](https://www.nuget.org/packages/Microsoft.VisualStudio.SDK/)) to confirm `17.14.40265` is still the latest 17.x stable.
