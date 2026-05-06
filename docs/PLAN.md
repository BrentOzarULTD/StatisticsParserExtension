# Implementation Plan — Statistics Parser SSMS Extension

## Overview

Build a SSMS 22 extension from scratch. The repo currently contains only documentation. The extension reads the Messages tab output, parses `STATISTICS IO` / `STATISTICS TIME` output, and renders parsed results in a dockable WPF tool window.

Phases 1–6 (Core + Tests) run on any OS. Phases 7–11 require Windows with VS 2026 and SSMS 22.

---

## Design Decisions

| # | Decision | Choice |
|---|---|---|
| 1 | JS parser source | Fetch `parser.js` from GitHub when porting — it is authoritative; TECHNICAL.md is a summary only |
| 2 | `CompletionTimeRow` storage | Parser parses ISO 8601 string to `DateTimeOffset`; UI formats with local culture |
| 3 | Column zero-suppression | `IoGroup.Columns` excludes columns where every row is zero (matches FUNCTIONAL.md examples) |
| 4 | SSMS 22 NuGet SDK packages | `Microsoft.VisualStudio.SDK` 17.14.40265 + `Microsoft.VSSDK.BuildTools` 17.14.2120; install target `Microsoft.VisualStudio.Ssms [22.0,]` amd64. See [PHASE7-RESEARCH.md](PHASE7-RESEARCH.md) |
| 5 | Messages tab context menu GUID | Deferred — Messages tab is not an `IVsOutputWindow` pane in SSMS 22. Initial placement: `IDM_VS_CTXT_CODEWIN`; final placement discovered during Phase 7 experimental-hive prototyping. See [PHASE7-RESEARCH.md](PHASE7-RESEARCH.md) |
| 6 | Result presentation surface | Spike `spike/in-pane-tab` (2026-05-06) replaced the dockable tool window with a third tab inside `SqlScriptEditorControl.TabPageHost` (a WinForms `TabControl`) hosting a WPF `StatisticsParserControl` via `ElementHost`. Reflection on `TabPageHost` is the only fragile step; the rest is public WinForms API. See Phase 12 |

---

## Phase 1 — Solution Scaffold - COMPLETED

Create `StatisticsParserExtension.sln` with three projects:

| Project | Target | Purpose |
|---|---|---|
| `StatisticsParser.Core` | `netstandard2.0` | All parsing and model logic; no external deps |
| `StatisticsParser.Core.Tests` | `net8.0` | xUnit tests; references Core |
| `StatisticsParser.Vsix` | `net48`, x64 | SSMS extension; references Core |

Build configs: `Debug|x64` and `Release|x64` for Vsix; `AnyCPU` for Core and Tests.

**Verification**: `dotnet build StatisticsParserExtension.sln` compiles (empty projects).

---

## Phase 2 — Core Models & Enums - COMPLETED

All files in `source/StatisticsParser.Core/Models/`:

**Enums**
- `RowType.cs` — `None, IO, ExecutionTime, CompileTime, RowsAffected, Error, IOTotal, ExecutionTimeTotal, CompileTimeTotal, Info, CompletionTime`
- `IoColumn.cs` — `NotFound, Table, Scan, Logical, Physical, PageServer, ReadAhead, PageServerReadAhead, LobLogical, LobPhysical, LobPageServer, LobReadAhead, LobPageServerReadAhead, PercentRead, SegmentReads, SegmentSkipped`

**Interfaces**
- `IResultRow.cs` — `RowType RowType { get; }`

**Row types** (all implement `IResultRow`)
- `IoRow.cs` — one field per numeric `IoColumn` + `string TableName` + `double PercentRead`
- `IoGroupTotal.cs` — same numeric fields as `IoRow` + `string TableName`
- `IoGroup.cs` — `string TableId`, `List<IoColumn> Columns`, `List<IoRow> Data`, `IoGroupTotal Total`
- `TimeRow.cs` — `int CpuMs`, `int ElapsedMs`, `bool Summary`
- `TimeTotal.cs` — `int CpuMs`, `int ElapsedMs`
- `RowsAffectedRow.cs` — `int Count`
- `ErrorRow.cs` — `string Text`
- `InfoRow.cs` — `string Text`
- `CompletionTimeRow.cs` — `DateTimeOffset Timestamp`

**Result container**
- `ParseResult.cs` — `List<IResultRow> Data`, `int TableCount`, `ParseResultTotal Total`
- `ParseResultTotal.cs` — `TimeTotal ExecutionTotal`, `TimeTotal CompileTotal`, `IoGrandTotal IoTotal`
- `IoGrandTotal.cs` — `List<IoColumn> Columns`, `List<IoGroupTotal> Data` (per table, sorted alpha), `IoGroupTotal Total`

**Verification**: `dotnet build source/StatisticsParser.Core` compiles.

---

## Phase 3 — Parser Language - COMPLETED

`source/StatisticsParser.Core/Parsing/ParserLanguage.cs`

- Properties per the table in [TECHNICAL.md §4](TECHNICAL.md) "Language Support".
- Three static singletons: `ParserLanguage.English` (the default consumed by `ParseData` when callers don't supply one), `ParserLanguage.Spanish`, `ParserLanguage.Italian`. Values are hardcoded in C#, copied verbatim from the three upstream JSON files cited in TECHNICAL.md §4.
- `IoColumn DetermineIoColumn(string columnText)` — case-insensitive trim-then-match against each column-variant list; returns `IoColumn.NotFound` when nothing matches.
- Internal static `IReadOnlyList<ParserLanguage> All { get; }` — used by tests to round-trip every language; placeholder for the future auto-detect work referenced in [TODO.md](TODO.md).

**Verification**: `dotnet build source/StatisticsParser.Core` compiles. (Phase 6 adds the load-everything tests.)

---

## Phase 4 — Parser Engine - COMPLETED

`source/StatisticsParser.Core/Parsing/Parser.cs`

**Public API**: `public static ParseResult ParseData(string text, ParserLanguage lang)`

**Before implementing**: fetch `https://raw.githubusercontent.com/Jorriss/StatisticsParser/master/src/assets/js/modules/parser.js` and read it for edge-case authority.

**Internal helpers**:
- `DetermineRowType(string line, ParserLanguage lang) → RowType`
- `ParseIoLine(string line, ParserLanguage lang) → IoRow`
- `ParseTimeLine(string line, ParserLanguage lang) → (int cpuMs, int elapsedMs)`
- `DetermineSummaryRow(TimeRow row, TimeTotal executionTotal, TimeTotal compileTotal) → bool`
- `ProcessGrandTotal(IoRow row, List<IoGroupTotal> grandTotal)`
- `FinalizeIoGroup(IoGroup group)`

**Algorithm** (iterate lines, track `prevRowType` and `currentGroup`):

| Event | Action |
|---|---|
| Non-IO row follows IO rows | Close group: compute totals, `% Logical Reads` (3 dp), append `PercentRead` to `Columns` |
| IO line with `SegmentReads/SegmentSkipped > 0` | Merge into last `IoRow`; do not append new row |
| `CompileTime` / `ExecutionTime` header | Advance `i` by 1; read data line |
| `ExecutionTime` data | Call `DetermineSummaryRow` (±5ms tolerance on elapsed); set `Summary = true` if matches |
| `Error` line | Append two `ErrorRow` objects (line `i` and `i+1`); advance `i` |
| After each `IoRow` appended | Call `ProcessGrandTotal` to merge into cross-statement accumulator |
| End of input | Recalculate grand total `% Logical Reads`; sort by table name alpha |

Zero-column suppression: columns where every row in the group is zero are excluded from `IoGroup.Columns`.

**Verification**: Parser compiles (tests come in Phase 6).

---

## Phase 5 — Formatting Utilities - COMPLETED

`source/StatisticsParser.Core/Formatting/TimeFormatter.cs`
- `public static string FormatMs(int ms)` → `"hh:mm:ss.fff"` (e.g. `959` → `"00:00:00.959"`)

`source/StatisticsParser.Core/Formatting/PercentFormatter.cs`
- `public static string FormatPercent(double value)` → `"100.000%"` (3 decimal places)

Tests live alongside (flat at `StatisticsParser.Core.Tests` root, matching `ParserTests.cs`):
- `TimeFormatterTests.cs` — `[Theory]` covering `0`, `5`, `959`, `1000`, `60000`, `3661123`
- `PercentFormatterTests.cs` — `[Theory]` covering `0.0`, `7.692`, `61.538`, `100.0`, `12.3456` (rounding); plus a `de-DE` `CurrentCulture` test confirming output stays invariant

---

## Phase 6 — Unit Tests - COMPLETED

`source/StatisticsParser.Core.Tests/ParserTests.cs`

Test inputs and expected values taken directly from FUNCTIONAL.md examples:

| Test | Key assertions |
|---|---|
| Single statement — IO | 1 `IoGroup`, 1 `IoRow` (Posts, logical=32, physical=3, readahead=1957), Total logical=32, `PercentRead=100.000%` |
| Single statement — time | 1 non-Summary `TimeRow` (cpu=0ms, elapsed=959ms); `ExecutionTotal`=(0ms, 959ms) |
| Multi statement — IO groups | 2 `IoGroup` objects; stmt 1: Posts 80.000% + Users 20.000%; stmt 2: Comments 100.000% |
| Multi statement — grand totals | Comments 61.538%, Posts 30.769%, Users 7.692%; grand logical=104 |
| Multi statement — time | Grand execution: cpu=8ms, elapsed=40ms |
| Empty input | `Data` is empty; no exception |
| No recognized statistics | Only `InfoRow` entries |
| Segment reads merge | Segment values merged into last `IoRow`; no extra row added |
| Summary row detection | Summary execution time not added to `ExecutionTotal` |
| All language singletons load | `ParserLanguage.English/Spanish/Italian` non-null; every column-variant list non-empty; `LangValue` matches `"en"/"es"/"it"` |
| Column name case-insensitivity | `English.DetermineIoColumn("Scan Count")` == `English.DetermineIoColumn("scan count")` == `IoColumn.Scan` |
| Spanish smoke parse | A small Spanish input (Tabla / lecturas lógicas / Tiempo de CPU) with `ParserLanguage.Spanish` produces an equivalent `ParseResult` to the English baseline |
| Italian smoke parse | Same test for Italian (Tabella / letture logiche / tempo di CPU) with `ParserLanguage.Italian` |

**Verification**: `dotnet test` — all tests pass.

---

## Phase 7 — VSIX Package Setup *(Windows only)* - COMPLETED

Research spikes complete — see [PHASE7-RESEARCH.md](PHASE7-RESEARCH.md). The Messages-tab-specific menu GUID is not statically discoverable; Phase 7 ships with a fallback `IDM_VS_CTXT_CODEWIN` placement plus a Tools menu entry, and the Messages-tab placement is added later via experimental-hive discovery (see Phase 8a below).

**Build constraint discovered during implementation**: SDK-style csproj + `Microsoft.VSSDK.BuildTools` 17.14.x does not auto-import `Microsoft.VsSDK.targets` (only the env-var-setting `Microsoft.VSSDK.BuildTools.targets` is auto-imported). The csproj manually `<Import>`s the inner `tools/VSSDK/Microsoft.VsSDK.targets` after the SDK targets resolve `$(IntermediateOutputPath)`, then chains `CreateVsixContainer` to `AfterTargets="Build"` so a `.vsix` lands in `bin/` on every build. Net effect: build with `msbuild` from a VS 2022/2026 install; `dotnet build` produces the assembly but skips VSIX packaging.

### csproj additions

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

### Files to create
- `source/StatisticsParser.Vsix/source.extension.vsixmanifest` — identity, version, `<InstallationTarget Id="Microsoft.VisualStudio.Ssms" Version="[22.0,]">` with `<ProductArchitecture>amd64</ProductArchitecture>`, `Microsoft.VisualStudio.Component.CoreEditor [17.0,19.0)` prerequisite
- `source/StatisticsParser.Vsix/StatisticsParser.vsct` — command group GUID + command ID; initial placement under `IDM_VS_CTXT_CODEWIN` (`guidSHLMainMenu`); a Messages-tab `<CommandPlacement>` is added in Phase 8a once the parent menu's `Guid`/`ID` is captured from the experimental hive
- `source/StatisticsParser.Vsix/Commands/StatisticsParserPackage.cs` — `AsyncPackage` subclass with `[PackageRegistration]`, `[ProvideMenuResource]`, `[ProvideToolWindow]` attributes; `InitializeAsync` registers command and tool window
- `source/StatisticsParser.Vsix/Commands/ParseStatisticsCommand.cs` — `Execute` handler: capture → parse → show window

**Verification**: VSIX loads in SSMS 22 experimental instance; "Parse Statistics" appears on the right-click context menu of an open .sql query window. **Correction (Phase 8c)**: SSMS 22's `SqlScriptEditorControl` does not honor the standard VS shell `IDM_VS_CTXT_CODEWIN`; the right-click placement was inert as shipped in Phase 7. Phase 8c adds an SSMS-specific `<CommandPlacement>` parented to `queryWindowContextCommandSet (33F13AC3-…) / queryWindowContextMenu (0x0050)`, which is the correct target. See [PHASE8C-FINDINGS.md](PHASE8C-FINDINGS.md). Messages-tab placement is deferred to a future enhancement.

---

## Phase 8 — Messages Tab Content Capture

The original plan assumed `IVsOutputWindow.GetPane()` would return the Messages tab. [PHASE7-RESEARCH.md](PHASE7-RESEARCH.md) §Spike 2 confirmed this is not the case in SSMS 22 — the Messages tab is owned by `SQLEditors.dll`'s `SqlScriptEditorControl`, not the VS Output window. Phase 8 is therefore split into a discovery prototype and an implementation pass.

### Phase 8a — Messages-text accessor discovery prototype *(experimental hive)*

Goal: identify, by hands-on probing, **one** working surface that returns the current Messages-tab text from the active query window. Try these in order; stop at the first that works:

1. **Brokered services** — inspect `Microsoft.SqlServer.Management.UI.VSIntegration.SqlEditor.BrokeredContracts.dll` (v22.0.103.0, ships in SSMS 22 IDE root) via reflection for any contract exposing messages/results text. Newest and most-likely-supported surface.
2. **`IVsTextBuffer` underneath the Messages tab** — locate the active `SqlScriptEditorControl` via DTE's active document; check whether the Messages tab's underlying control implements `IVsTextLines` / `IVsTextBuffer` we can `GetLineText()` from.
3. **Reflection on `SqlScriptEditorControl`** — private API; almost certainly has a `MessagesText` / `MessagesPaneText` member. Fragile across SSMS versions; document the exact reflected member.
4. **Query-execution completion event** — subscribe via the SqlEditor brokered service and accumulate the `messages` payload as queries finish. Highest effort but most stable.

While investigating, also capture: the parent menu `Guid` + `Id` of the Messages-tab right-click context menu by attaching a debugger to `IOleCommandTarget.QueryStatus` callbacks while right-clicking the tab. Add the `<CommandPlacement>` to the vsct once captured.

**Output of 8a**: see [PHASE8A-FINDINGS.md](PHASE8A-FINDINGS.md) for the surface chosen, reflection signatures, and version-drift recovery notes. The Messages-tab menu Guid/Id capture is deferred to a follow-on Phase 8c task — Phase 7's Tools-menu placement is sufficient for 8b verification.

### Phase 8b — Implement `MessagesTabReader`

`source/StatisticsParser.Vsix/Commands/MessagesTabReader.cs`

```csharp
public static string GetMessagesText(IServiceProvider serviceProvider)
```

Implementation follows whichever surface 8a settled on. If the chosen surface returns text synchronously, the signature above is final; if it requires async accumulation (option 4), promote to `Task<string> GetMessagesTextAsync(...)` and update the caller.

**Verification**: captured text matches Messages tab content (manual smoke test in SSMS) for: a single-statement query with `SET STATISTICS IO, TIME ON`, a multi-statement batch, and a query that produces an error.

**Output of 8b**: see [PHASE8B-FINDINGS.md](PHASE8B-FINDINGS.md) — verification results (2026-05-04) and Phase 8c briefing.

### Phase 8c — Right-click placement - COMPLETED

Adds the .sql query body right-click placement and tears down the Phase 8a/8b discovery scaffolding. The Phase 7 verification claim that `Parse Statistics` appeared on .sql query body right-click was incorrect — SSMS 22's `SqlScriptEditorControl` does not honor `IDM_VS_CTXT_CODEWIN`; the placement was inert. The fix uses the SSMS-specific `queryWindowContextMenu` from `queryWindowContextCommandSet (33F13AC3-80BB-4ECB-85BC-225435603A5E)`, sourced from the open-source [SqlFormatter](https://github.com/madskristensen/SqlFormatter) extension's vsct.

**Discovery dead-end (Messages-tab menu)**: two passive `IOleCommandTarget.QueryStatus` capture passes proved the Messages-tab right-click menu exists and is OLE-routed (clean 5-fire fingerprint of 17 commands across 9 command sets), but `QueryStatus` only surfaces commands inside a menu, never the menu's own IDM. Targeted byte/text scans of SSMS editor DLLs and Command Explorer browsing also did not surface the IDM. Deferred to future work — see [PHASE8C-FINDINGS.md](PHASE8C-FINDINGS.md) §Deferred.

**Verification**: right-click in the .sql query body in SSMS 22 → `Parse Statistics` appears, opens the docked **Stats Parser** tool window with captured text matching `Tools → Parse Statistics` byte-for-byte. All three [PHASE8B-FINDINGS.md §Smoke-test results](PHASE8B-FINDINGS.md) scenarios re-verified via the new entry point. `Tools → Dump Menu Capture` no longer exists; `Tools → Parse Statistics` still works as a fallback.

**Output of 8c**: see [PHASE8C-FINDINGS.md](PHASE8C-FINDINGS.md).

---

## Phase 9 — Tool Window & WPF UI — SUPERSEDED by Phase 12

The dockable `ToolWindowPane` shell described below was built but replaced by the in-pane tab architecture in Phase 12. The WPF rendering plan in this section (DataGrids, dynamic columns, totals section) is still the target — it just lives inside an `ElementHost` inside a `TabPage` inside `SqlScriptEditorControl.TabPageHost` instead of inside a `ToolWindowPane`.

**Shell** *(historical — replaced)*
- `source/StatisticsParser.Vsix/Windows/StatisticsParserToolWindow.cs` — `ToolWindowPane` subclass hosting `StatisticsParserControl`
- `source/StatisticsParser.Vsix/Controls/StatisticsParserControl.xaml` + `.xaml.cs` — `ScrollViewer > StackPanel`; public `Render(ParseResult result)` method

**Rendering each `IResultRow` type** (in `Data` order):

| Type | WPF element |
|---|---|
| `RowsAffectedRow` | Bold `TextBlock` — "N rows affected" |
| `IoGroup` | `DataGrid` — dynamic columns from `IoGroup.Columns`; Total row pinned at bottom |
| `TimeRow` (non-Summary) | Row in current time `DataGrid` |
| `ErrorRow` | `TextBlock` — red foreground |
| `InfoRow` | `TextBlock` — plain |
| `CompletionTimeRow` | `TextBlock` — `Timestamp.ToLocalTime()` formatted with local culture |

**After all statements**: "Totals:" label, grand IO `DataGrid` from `IoTotal`, grand time `DataGrid` from `ExecutionTotal` + `CompileTotal`.

**Dynamic DataGrid columns**: `AutoGenerateColumns = false`; programmatically add `DataGridTextColumn` for each `IoColumn` in `Columns`. Display name map:

```
Scan → "Scan Count" | Logical → "Logical Reads" | Physical → "Physical Reads"
ReadAhead → "Read-Ahead Reads" | PageServer → "Page Server Reads"
PageServerReadAhead → "Page Server Read-Ahead Reads"
LobLogical → "LOB Logical Reads" | LobPhysical → "LOB Physical Reads"
LobPageServer → "LOB Page Server Reads" | LobReadAhead → "LOB Read-Ahead Reads"
LobPageServerReadAhead → "LOB Page Server Read-Ahead Reads"
SegmentReads → "Segment Reads" | SegmentSkipped → "Segment Skipped"
PercentRead → "% Logical Reads of Total Reads"
```

**Empty state**: centered `TextBlock` — "No statistics output found in Messages tab."

**Verification**: End-to-end smoke test in SSMS with single-statement and multi-statement Statistics IO/TIME output.

---

## Phase 10 — Theme Support

- All colors via `DynamicResource` bound to VS environment resource keys (e.g. `EnvironmentColors.ToolWindowBackgroundColorKey`).
- No hardcoded color values anywhere in XAML.
- DataGrid row, header, and border colors bound to VS theme keys.
- Error text uses `EnvironmentColors.StatusBarErrorColorKey` or equivalent.

**Verification**: Switch SSMS theme (Tools > Options > Environment > General) while tool window is open; colors update live.

---

## Phase 11 — Build & CI

`.github/workflows/build.yml` on `windows-latest`:

1. Checkout
2. Setup MSBuild (VS 2026)
3. `nuget restore`
4. `dotnet test source/StatisticsParser.Core.Tests` — Core tests run without SSMS
5. `msbuild source/StatisticsParser.Vsix /p:Configuration=Release /p:Platform=x64`
6. Upload `StatisticsParser.vsix` as build artifact

**Verification**: GitHub Actions run produces green build and downloadable VSIX artifact.

---

## Phase 12 — In-Pane Tab Architecture - SPIKE COMPLETED (branch `spike/in-pane-tab`)

Replaces the Phase 9 dockable tool-window with a third tab — labeled **"Parse Statistics"** — sitting inside the SSMS query window's Results/Messages tab strip. The change was scoped as a throwaway spike on a branch; behavior validated, branch held back from merge pending real-use bake-in.

### Discovery path

Two-stage probe (deleted from the codebase after the spike confirmed feasibility):
1. **Visual-tree probe** (`Diagnostics/QueryWindowVisualTreeProbe.cs`, deleted): walked the WPF visual tree starting from `IVsMonitorSelection.SEID_DocumentFrame → __VSFPROPID.VSFPROPID_DocView` and from `Application.Current.MainWindow`. Confirmed the docView is `Microsoft.SqlServer.Management.UI.VSIntegration.Editors.SqlScriptEditorControl` and that no `System.Windows.Controls.TabControl` for Results/Messages exists in the WPF tree.
2. **DocView reflection + Win32 child enumeration**: dumped all non-trivial members of `SqlScriptEditorControl` and walked Win32 child windows of every `HwndHost`. Confirmed:
   - `SqlScriptEditorControl.TabPageHost` (public property) is a `DisplaySqlResultsTabControl` instance.
   - Win32 child windows of the editor's `GenericPaneClientHwndHost` include a `WindowsForms10.SysTabControl32` and adjacent `WindowsForms10.Window` children with text `"Results"` and `"Messages"`. → confirmed WinForms-hosted tab strip; standard `TabControl.TabPages.Add(...)` is the injection path.

### Implementation

| Component | Path |
|---|---|
| Entry point | [InPaneTab/ResultsTabInjector.cs](../source/StatisticsParser.Vsix/InPaneTab/ResultsTabInjector.cs) — resolves docView, hands off to a per-docView supervisor |
| Supervisor | [InPaneTab/TabPageSupervisor.cs](../source/StatisticsParser.Vsix/InPaneTab/TabPageSupervisor.cs) — owns the `TabPage`, hooks query-completion events, auto-refreshes |

Stored in a `ConditionalWeakTable<object, TabPageSupervisor>` keyed on docView so supervisor + event subscriptions get GC'd together with the docView when the query window closes (no explicit unhook needed).

**Auto-refresh hook**: heuristic event matching at runtime. The supervisor enumerates events on `SqlScriptEditorControl` and on its `m_sqlResultsControl` (a `DisplaySQLResultsControl`) and subscribes to any whose name contains `Completed | Executed | Finished | Stopped | Done`. Dynamically-built delegates via `Expression.Lambda(delegateType, ...)` adapt arbitrary signatures. The diagnostics pane reports a failure only if zero events matched (i.e. auto-refresh would be silently broken).

**Re-injection**: SSMS clears its tab strip on query start, so on every completion event the supervisor re-resolves `TabPageHost`, re-creates the `TabPage` if missing, re-captures Messages text via the brokered service, and re-renders the WPF control. Selection is left to SSMS's F5-default focus.

### Behavior verified (2026-05-06)

| Scenario | Expected | Actual |
|---|---|---|
| Right-click → Parse Statistics, first time | New "Parse Statistics" tab appears, content shown, tab selected | ✓ |
| Re-run query (F5) | Tab persists / is re-added; content auto-refreshes | ✓ |
| F5 while on Messages | Tab content updates silently; user stays on Messages | ✓ |
| F5 while on Parse Statistics | SSMS focuses Results (default behavior); Parse Statistics content still updates | ✓ |

### Open work before merging spike branch to `dev`

- Real-use bake-in across multiple query windows / re-execution / window close-and-reopen
- Phase 9 structured-DataGrid rendering (`StatisticsParserControl.Render(ParseResult)`) still pending — currently the in-pane tab shows the minimum-viable text dump from the Phase 8b ship
- Phase 10 theme support — verify VS resource keys flow across the `ElementHost` boundary
- Update `docs/FUNCTIONAL.md` if the user-visible UX description still mentions a dockable tool window

---

## Remaining Risks

| Risk | Mitigation |
|---|---|
| Messages tab is **not** an `IVsOutputWindow` pane in SSMS 22 (confirmed 2026-04-29 via install inspection — see [PHASE7-RESEARCH.md](PHASE7-RESEARCH.md)) | Phase 8a probes brokered contracts → `IVsTextBuffer` → reflection on `SqlScriptEditorControl` → query-completion event in that order; first one that works wins |
| Messages-tab context menu Guid/Id is not statically discoverable (no text VSCT/CTC ships with SSMS 22) | Phase 7 ships with `IDM_VS_CTXT_CODEWIN` placement only; Phase 8a captures the Messages-tab parent menu Guid/Id via debugger-attached `IOleCommandTarget` probing in the experimental hive and adds a `<CommandPlacement>` |
| `Microsoft.VisualStudio.SDK` v18 metapackage not yet on NuGet (SSMS 22 ships shell 18.0) | Pin to 17.14.40265 — Microsoft Learn confirms VS 2026 accepts 17.x extensions with minimal breaking changes; revisit when v18 is published |
| Reflection-based Messages-text access (Phase 8a option 3) is fragile across SSMS minor versions | Prefer brokered-contracts (option 1) or completion-event (option 4) if either works; if reflection is the only path, pin the signature in code with a clear comment so version bumps surface as test failures |
| Phase 12: `SqlScriptEditorControl.TabPageHost` property name is reflection-only and could be renamed in an SSMS minor version | `TabPageSupervisor.GetTabPageHost` tries the public property first, then falls back to `tabPagesHost` / `m_tabPagesHost` private fields; throws a descriptive exception that surfaces in the diagnostics pane when none are found |
| Phase 12: heuristic event-name matching (`Completed`/`Executed`/`Finished`/`Stopped`/`Done`) could miss the actual query-completion event after an SSMS update | The supervisor writes a clear failure to the diagnostics pane when zero events matched; user-visible symptom is "tab works on first invocation, but doesn't auto-refresh". Recovery is to re-add an event-enumeration debug pass (see deleted [QueryWindowVisualTreeProbe](../source/StatisticsParser.Vsix/Diagnostics/) for the pattern) and refine the pattern list |

---

## Complete File List

```
StatisticsParserExtension.sln
source/
  StatisticsParser.Core/
    StatisticsParser.Core.csproj
    Models/
      RowType.cs, IoColumn.cs, IResultRow.cs
      IoRow.cs, IoGroupTotal.cs, IoGroup.cs
      TimeRow.cs, TimeTotal.cs
      RowsAffectedRow.cs, ErrorRow.cs, InfoRow.cs, CompletionTimeRow.cs
      ParseResult.cs, ParseResultTotal.cs, IoGrandTotal.cs
    Parsing/
      ParserLanguage.cs
      Parser.cs
    Formatting/
      TimeFormatter.cs
      PercentFormatter.cs
  StatisticsParser.Core.Tests/
    StatisticsParser.Core.Tests.csproj
    ParserTests.cs
    ParserLanguageTests.cs
    ParserSpanishTests.cs
    ParserItalianTests.cs
    TimeFormatterTests.cs
    PercentFormatterTests.cs
  StatisticsParser.Vsix/
    StatisticsParser.Vsix.csproj
    source.extension.vsixmanifest
    VSCommandTable.vsct
    StatisticsParserPackage.cs
    Commands/
      ParseStatisticsCommand.cs
    Capture/
      ContractTypes.cs
      MessagesBrokeredClient.cs
      MessagesCaptureResult.cs
      MessagesTabReader.cs
    InPaneTab/
      ResultsTabInjector.cs
      TabPageSupervisor.cs
    Controls/
      StatisticsParserControl.xaml
      StatisticsParserControl.xaml.cs
    Diagnostics/
      StatisticsParserDiagnosticsPane.cs
.github/workflows/build.yml
```
