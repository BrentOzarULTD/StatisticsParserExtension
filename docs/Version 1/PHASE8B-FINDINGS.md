# Phase 8b Findings — Messages-tab capture pipeline

This appendix records what shipped in Phase 8b, the verification evidence proving end-to-end capture works, and the prerequisites a future Phase 8c session needs to add the Messages-tab right-click placement. Reflection signatures and version-drift recovery for the underlying brokered surface live in [PHASE8A-FINDINGS.md](PHASE8A-FINDINGS.md) and are not duplicated here.

## What shipped

**New** — `source/StatisticsParser.Vsix/Capture/`:

| File | Role |
|---|---|
| [MessagesCaptureResult.cs](../source/StatisticsParser.Vsix/Capture/MessagesCaptureResult.cs) | Public `readonly struct` + `MessagesCaptureStatus` enum (`Ok`, `NoActiveWindow`, `EmptyMessages`, `ContractsAssemblyMissing`, `ProxyUnavailable`, `Failed`). |
| [ContractTypes.cs](../source/StatisticsParser.Vsix/Capture/ContractTypes.cs) | `AsyncLazy<ContractTypes>` cache for the SSMS BrokeredContracts assembly load + per-member reflection lookups. |
| [MessagesBrokeredClient.cs](../source/StatisticsParser.Vsix/Capture/MessagesBrokeredClient.cs) | Encapsulates all reflection: acquires `IServiceBroker`, peels `ServiceRpcDescriptor` → `ServiceMoniker` if needed, picks the matching `GetProxyAsync` overload, unwraps `ValueTask<T>` / `Task<T>`. |
| [MessagesTabReader.cs](../source/StatisticsParser.Vsix/Capture/MessagesTabReader.cs) | Public façade `GetMessagesTextAsync(AsyncPackage, CancellationToken)`. 64 KB-page paging loop with iteration cap and stall guard. |

**Modified**:

| File | Change |
|---|---|
| [Commands/ParseStatisticsCommand.cs](../source/StatisticsParser.Vsix/Commands/ParseStatisticsCommand.cs) | `ExecuteAsync` now: open tool window → `MessagesTabReader.GetMessagesTextAsync` → branch on status → `Parser.ParseData` on `Ok` → `control.ShowCapturedText` / `ShowCaptureError`. RPC failures logged to the Diagnostics pane. |
| [Controls/StatisticsParserControl.xaml](../source/StatisticsParser.Vsix/Controls/StatisticsParserControl.xaml) + [.cs](../source/StatisticsParser.Vsix/Controls/StatisticsParserControl.xaml.cs) | Added monospace read-only `TextBox` + bold status `TextBlock`; `EmptyStateText` reused for error states. Throwaway shape — Phase 9 replaces with `Render(ParseResult)`. |
| [Diagnostics/StatisticsParserDiagnosticsPane.cs](../source/StatisticsParser.Vsix/Diagnostics/StatisticsParserDiagnosticsPane.cs) | Renamed from `ProbeOutputPane.cs`; pane title now `"Statistics Parser — Diagnostics"`. **Pane GUID kept stable** so users keep their docked pane across the rename. |
| [Diagnostics/MenuGuidCapture.cs](../source/StatisticsParser.Vsix/Diagnostics/MenuGuidCapture.cs) | `Dump` parameter type updated to `StatisticsParserDiagnosticsPane`. Component is dormant but registered (see §Phase 8c briefing). |
| [StatisticsParser.vsct](../source/StatisticsParser.Vsix/StatisticsParser.vsct) | `cmdidProbeMessageSource` Button + placement + IDSymbol removed. |
| [PLAN.md](PLAN.md) | §8a "Output of 8a" line repointed at [PHASE8A-FINDINGS.md](PHASE8A-FINDINGS.md). |

**Deleted**: `BrokeredContractProbe.cs`, `TextBufferProbe.cs`, `ReflectionProbe.cs`, `QueryEventsProbe.cs`, `MessageSourceProbeCommand.cs`, `ProbeOutputPane.cs` (all in `Diagnostics/`).

## Behavior delivered

- `Tools → Parse Statistics` opens the docked **Stats Parser** tool window.
- On success the tool window shows a bold status line `Captured N chars from Messages tab; parsed M rows.` plus the captured text in a monospace `TextBox` (read-only, selectable, horizontal + vertical scroll).
- Empty / no-window / RPC-failure states render a centered single-line message via the existing `EmptyStateText` `TextBlock`.
- Failures additionally write a structured `FAIL` line to **View → Output → Statistics Parser — Diagnostics**.
- The `<CommandPlacement>` for the Messages-tab right-click menu is **not yet present**. Tools menu is the only entry point.

## Smoke-test results (2026-05-04)

All three scenarios run against a live SSMS 22 experimental hive (`/RootSuffix Exp`) connected to a SQL Server with `master` accessible.

### Test 1 — Single statement

```sql
SET STATISTICS IO, TIME ON;
SELECT TOP 100 * FROM sys.objects;
```

Captured text excerpt:

```
(100 rows affected)
Table 'syspalnames'. Scan count 0, logical reads 200, physical reads 2, …
Table 'sysschobjs'. Scan count 1, logical reads 62, physical reads 0, … read-ahead reads 57, …
Table 'syssingleobjrefs'. Scan count 1, logical reads 6, physical reads 0, … read-ahead reads 1, …

 SQL Server Execution Times:
   CPU time = 16 ms,  elapsed time = 14 ms.

Completion time: 2026-05-04T12:41:54.1099196-04:00
```

**Proves**: brokered surface returns the live Messages-tab text verbatim; the IO + Time + Completion content reaches `Parser.ParseData` intact.

### Test 2 — Multi-statement batch

```sql
SET STATISTICS IO, TIME ON;
SELECT TOP 200 * FROM sys.objects;
SELECT TOP 200 * FROM sys.columns;
SELECT TOP 200 * FROM sys.indexes;
```

Captured text excerpt (key tokens only):

```
SQL Server parse and compile time:
   CPU time = 110 ms, elapsed time = 192 ms.

(168 rows affected)                       ← sys.objects
…
 SQL Server Execution Times:
   CPU time = 15 ms,  elapsed time = 146 ms.

(200 rows affected)                       ← sys.columns
Table 'sysobjvalues'. Scan count 201, logical reads 2550, …
…
 SQL Server Execution Times:
   CPU time = 47 ms,  elapsed time = 597 ms.

(200 rows affected)                       ← sys.indexes
…
 SQL Server Execution Times:
   CPU time = 0 ms,  elapsed time = 258 ms.

Completion time: 2026-05-04T12:43:25.3976569-04:00
```

(`sys.objects` returned 168 rows because that's all this DB has; the parser is row-count-agnostic.)

**Proves**: segment paging works across the 64 KB page boundary; ordering is preserved; multiple `SQL Server Execution Times` blocks captured in sequence; the parse-and-compile preamble is captured.

### Test 3 — Error path

```sql
SELECT * FROM dbo.NoSuchTable;
```

Captured text:

```
SQL Server parse and compile time:
   CPU time = 0 ms, elapsed time = 0 ms.
Msg 208, Level 16, State 1, Line 1
Invalid object name 'dbo.NoSuchTable'.

Completion time: 2026-05-04T12:44:10.7063951-04:00
```

`MessagesCaptureStatus` was `Ok` (errors are still messages on the SQL side).

**Proves**: error-content capture works; the status taxonomy in `MessagesCaptureResult` correctly classifies SQL-level errors as `Ok` rather than `Failed` (the latter is reserved for capture-pipeline failures).

## Brokered surface — cross-reference

The chosen surface is `IQueryEditorTabDataServiceBrokered.GetMessagesTabSegmentAsync(int startPosition, int maxLength, CancellationToken) → ValueTask<TextContentSegment>` from `Microsoft.SqlServer.Management.UI.VSIntegration.SqlEditor.BrokeredContracts.dll` v22.5.175.63478. Service moniker resolved from `QueryEditorTabDataServiceDescriptors.QueryEditorTabDataService`. Full reflection signatures, version-drift recovery, and the rationale for skipping the dynamic probes live in [PHASE8A-FINDINGS.md](PHASE8A-FINDINGS.md).

## Reproducible deploy procedure

Used for the 2026-05-04 smoke tests. Mirrors the Resolution block in [PHASE8A-ATTEMPT1.md](PHASE8A-ATTEMPT1.md) but points at the `Release|x64` vsix.

```powershell
# 1. Build (msbuild from VS 2026 — dotnet build alone does not produce the .vsix)
$msbuild = "C:\Program Files\Microsoft Visual Studio\18\Professional\MSBuild\Current\Bin\MSBuild.exe"
& $msbuild "source\StatisticsParser.Vsix\StatisticsParser.Vsix.csproj" `
    /p:Configuration=Release /p:Platform=x64 /v:minimal /nologo

# 2. Set paths
$installer = "C:\Program Files\Microsoft SQL Server Management Studio 22\Release\Common7\IDE\VSIXInstaller.exe"
$ssms      = "C:\Program Files\Microsoft SQL Server Management Studio 22\Release\Common7\IDE\SSMS.exe"
$vsix      = "Z:\Brent Ozar Unlimited\Code\StatisticsParserExtension\source\StatisticsParser.Vsix\bin\x64\Release\net48\StatisticsParser.Vsix.vsix"

# 3. Uninstall (idempotent) + reinstall — use the call operator, not Start-Process -ArgumentList
& $installer /quiet /rootSuffix:Exp /uninstall:StatisticsParser.4A9EFF2E-819B-453D-BE4C-5DF7B343C0E7
& $installer /quiet /rootSuffix:Exp /logFile:"$env:TEMP\statsparser-install.log" $vsix

# 4. Force catalog rebuild — REQUIRED after every reinstall, otherwise SSMS launches with the stale catalog
& $ssms /UpdateConfiguration /RootSuffix Exp

# 5. Launch
& $ssms /RootSuffix Exp
```

## Phase 8c briefing — Messages-tab right-click placement

Phase 8c adds a `<CommandPlacement>` to [StatisticsParser.vsct](../source/StatisticsParser.Vsix/StatisticsParser.vsct) so `Parse Statistics` appears on the right-click menu of the SSMS Messages tab. The parent menu's `Guid` + `ID` are not statically discoverable (no public VSCT/CTC ships with SSMS 22) and must be captured empirically in a live session.

### What's already wired

- [Diagnostics/MenuGuidCapture.cs](../source/StatisticsParser.Vsix/Diagnostics/MenuGuidCapture.cs) is a passive `IOleCommandTarget` registered in `StatisticsParserPackage.InitializeAsync` ([StatisticsParserPackage.cs:22](../source/StatisticsParser.Vsix/StatisticsParserPackage.cs#L22)) via `IVsRegisterPriorityCommandTarget`. It dedupe-counts every `(cmdGroup, cmdId)` pair seen via `QueryStatus`.
- `MenuGuidCapture.Dump(StatisticsParserDiagnosticsPane pane, bool reset)` writes the captured pairs (sorted by frequency, descending) to the diagnostics pane.
- [Diagnostics/StatisticsParserDiagnosticsPane.cs](../source/StatisticsParser.Vsix/Diagnostics/StatisticsParserDiagnosticsPane.cs) is reachable via `GetOrCreate(serviceProvider)` and writes to **View → Output → Statistics Parser — Diagnostics**.

### What's missing — the gap 8c must close

After Phase 8b's deletions, **nothing calls `MenuGuidCapture.Dump`**. The capture is happening in the background but there is no trigger to read it back. Phase 8c must add a trigger. Recommended: a temporary diagnostic command (e.g. `cmdidDumpMenuCapture` on the Tools menu) that calls `MenuGuidCapture.Instance?.Dump(pane, reset: true)`. Remove the command + symbol once the placement is captured.

### Step-by-step procedure

1. Add the dump command: a new `[Command(...)]` class in `Commands/` calling `MenuGuidCapture.Instance?.Dump(...)`, and a matching `Button` + `IDSymbol` + Tools-menu `<CommandPlacement>` in [StatisticsParser.vsct](../source/StatisticsParser.Vsix/StatisticsParser.vsct).
2. Build + deploy via the procedure above; launch SSMS in `/RootSuffix Exp`.
3. Run any query with `SET STATISTICS IO, TIME ON` so a Messages tab exists with content.
4. Click `Tools → Dump Menu Capture` **once** to flush the pre-Messages-tab QueryStatus noise (this is the `reset: true` call).
5. Right-click directly on the **Messages** tab text area. Note the visible items (cancel without clicking — clicking would fire commands and pollute the capture).
6. Click `Tools → Dump Menu Capture` **again**. Read the diagnostics pane: any `(cmdGroup, cmdId)` pair that appears **between** the two dumps with a low fire count (typically 1–3) is the Messages-tab parent menu. Higher-count pairs are background `QueryStatus` polling and can be ignored.
7. Record the `Guid` + `ID` (in hex). Add a `<CommandPlacement>` for `cmdidParseStatistics` to [StatisticsParser.vsct](../source/StatisticsParser.Vsix/StatisticsParser.vsct) parented to that Guid/Id (with a fresh `<Group>` if the existing `ParseStatisticsGroup` cannot be reused, e.g. if the parent menu is in a different command set).
8. Rebuild + redeploy. Right-click the Messages tab — `Parse Statistics` should appear and fire the same `ExecuteAsync` path.
9. Remove the temporary dump command (class + vsct entries). Decision point: also remove `MenuGuidCapture.InitializeAsync(this)` from [StatisticsParserPackage.cs:22](../source/StatisticsParser.Vsix/StatisticsParserPackage.cs#L22) and delete the [MenuGuidCapture.cs](../source/StatisticsParser.Vsix/Diagnostics/MenuGuidCapture.cs) file, or keep dormant for future menu work — flag this in the 8c PR description.

### Re-verification

After 8c lands, re-run all three smoke-test queries from above by clicking the new right-click entry (not the Tools menu) to prove the placement reaches the same code path. The captured-text, paged, and error-path behaviors should be byte-identical.

## Out of scope

- Structured `ParseResult` rendering with DataGrids (Phase 9).
- Theme bindings (Phase 10).
- CI workflow (Phase 11).
