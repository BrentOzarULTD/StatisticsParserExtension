# Statistics Parser SSMS Extension — Technical Requirements

> Status: extension renders parsed output as a third tab ("Parse Statistics") inside the
> SSMS query window's `SqlScriptEditorControl` Results/Messages tab strip. The original
> dockable tool-window approach described in the early architecture has been superseded —
> see §5 "In-Pane Results Tab" below.

## Target Environments

| SSMS Version | VS Shell | Bitness | .NET Target | Status |
|---|---|---|---|---|
| SSMS 22 (current) | VS 2026 | 64-bit | .NET Framework 4.8 | Supported |

SSMS 22 requires .NET Framework 4.8 to run. SSMS has not migrated to modern .NET.

## Technology Stack

- **Language**: C# (.NET Framework 4.8)
- **UI Framework**: WPF (Windows Presentation Foundation)
- **Extension Model**: VSIX / MEF (Managed Extensibility Framework)
- **SDK Packages**: SSMS Extension SDK NuGet packages (version-specific per build target)
- **Build System**: MSBuild / Visual Studio solution
- **VCS**: Git

## Solution Structure

The solution contains three projects:

```
StatisticsParserExtension.sln
└── source/
    ├── StatisticsParser.Core/         # .NET Standard 2.0 class library
    │   ├── Models/               # IResultRow, IoRow, IoGroup, TimeRow, etc.
    │   ├── Parsing/              # ParseData, DetermineRowType, ParserLanguage, etc.
    │   └── Formatting/           # ms → hh:mm:ss.ms, percent formatting
    │
    ├── StatisticsParser.Core.Tests/   # xUnit test project, targets net8.0
    │   └── Parsing/              # Parser unit tests
    │
    └── StatisticsParser.Vsix/         # VSIX targeting SSMS 22 (64-bit, VS2026 shell)
        ├── Commands/             # Right-click command handler (ParseStatisticsCommand)
        ├── Capture/              # Brokered-service Messages-tab text reader
        ├── InPaneTab/            # ResultsTabInjector + TabPageSupervisor (in-pane tab plumbing)
        ├── Controls/             # WPF UserControl rendered inside the in-pane tab
        ├── Diagnostics/          # Output-pane logger (silent on success)
        └── source.extension.vsixmanifest
```

`StatisticsParser.Core` targets .NET Standard 2.0, which is compatible with both the .NET Framework 4.8 VSIX host and the .NET 8 test project. All parsing and data model logic lives here; the VSIX project contains only SSMS integration code and WPF UI.

## Extension Architecture

SSMS extensions are MEF-based VSIX packages built on the Visual Studio Isolated Shell. The entry point is a `Package` class (subclass of `AsyncPackage` or `Package`) registered via attributes. SSMS loads the package on demand or at IDE startup depending on the `ProvideAutoLoad` rule set.

Key MEF/VS / SSMS-specific concepts used:
- **VSPackage**: The root package class, handles initialization and command registration.
- **.vsct file**: Defines the "Parse Statistics" command and its placement under the SSMS-specific `queryWindowContextCommandSet` (so right-click in the .sql query body shows it). A Tools-menu placement is included as a fallback entry point.
- **SSMS brokered service `IQueryEditorTabDataServiceBrokered`**: SSMS-22-shipped contracts assembly used to read Messages-tab text. Not a standard VS shell interface — see §3.
- **`SqlScriptEditorControl`**: SSMS-22 WinForms control hosted as the document view of a SQL query window. Its `TabPageHost` property exposes the Results/Messages WinForms `TabControl` into which we inject our own `TabPage`. Reflection-only — see §5.
- **`ElementHost` (WindowsFormsIntegration)**: bridges WPF content (our `StatisticsParserControl`) into the WinForms `TabPage`.

## Key Components

### 1. SSMS Package Registration

The `Package` class ([StatisticsParserPackage.cs](../source/StatisticsParser.Vsix/StatisticsParserPackage.cs)) bootstraps the extension. It:
- Registers the right-click command via `Community.VisualStudio.Toolkit`'s `RegisterCommandsAsync()`.
- Acquires SSMS services lazily as the command runs — there are no eagerly-resolved services and no tool window registration.

The `.vsct` declares two placements for the `Parse Statistics` command: SSMS-specific `queryWindowContextCommandSet (33F13AC3-80BB-4ECB-85BC-225435603A5E) / queryWindowContextMenu (0x0050)` for query-body right-click, and `IDG_VS_TOOLS_EXT_TOOLS` for a Tools-menu fallback. The query-body placement was discovered via Phase 8c — see [PLAN.md](PLAN.md) and [PHASE8C-FINDINGS.md](PHASE8C-FINDINGS.md).

### 2. Context Menu Integration

A single command — **"Parse Statistics"** — appears in the right-click context menu of the .sql query body, plus on the Tools menu as a fallback. (The Messages-tab right-click menu's GUID/ID were not statically discoverable — see [PHASE8C-FINDINGS.md](PHASE8C-FINDINGS.md).)

Behavior on invocation ([ParseStatisticsCommand.cs](../source/StatisticsParser.Vsix/Commands/ParseStatisticsCommand.cs)):
1. Capture the full text content of the Messages tab (see §3).
2. Pass the text to the Core parser.
3. Inject (or refresh) a "Parse Statistics" tab inside the query window's Results/Messages tab strip and render parsed output into it (see §5).

On the first invocation per query window, the supervisor also subscribes to SSMS query-completion events so subsequent query executions auto-refresh the tab content without requiring another right-click. Failures are reported to the **Statistics Parser — Diagnostics** Output pane; the pane stays empty when everything works.

### 3. Messages Tab Content Capture

The Messages tab in SSMS 22 is **not** an `IVsOutputWindow` pane. It lives inside `SqlScriptEditorControl` (owned by `SQLEditors.dll`) and is unreachable from any standard VS shell extension surface — see [PHASE7-RESEARCH.md](PHASE7-RESEARCH.md) §Spike 2 for the empirical proof. Capture is instead performed via SSMS's brokered service surface, discovered in Phase 8a.

**Path** ([Capture/](../source/StatisticsParser.Vsix/Capture/)):

1. `Microsoft.SqlServer.Management.UI.VSIntegration.SqlEditor.BrokeredContracts.dll` is loaded by reflection from the SSMS install directory ([ContractTypes.cs](../source/StatisticsParser.Vsix/Capture/ContractTypes.cs)).
2. `SVsBrokeredServiceContainer` provides a full-access `IServiceBroker`. We acquire a proxy for `ISqlEditorServiceBrokered` (via `SqlEditorBrokeredServiceDescriptors.SqlEditorService`) and call `GetCurrentConnectionAsync` to obtain a `SqlEditorConnectionDetails` whose `EditorMoniker` identifies the active SQL editor. Empty/null moniker → `MessagesCaptureStatus.NoActiveWindow` ([MessagesBrokeredClient.cs](../source/StatisticsParser.Vsix/Capture/MessagesBrokeredClient.cs)).
3. With that moniker in hand, we acquire a second proxy for `IQueryEditorTabDataServiceBrokered` (via `QueryEditorTabDataServiceDescriptors.QueryEditorTabDataService`).
4. The proxy exposes `GetMessagesTabSegmentAsync(string editorMoniker, int start, int max, CancellationToken)` returning `TextContentSegment { Content, StartPosition, TotalLength }`. We page through 64 KB segments until `TotalLength` characters have been read ([MessagesTabReader.cs](../source/StatisticsParser.Vsix/Capture/MessagesTabReader.cs)).
5. The captured text is a snapshot at command-fire time. If a query is still running and the tab continues to grow, only the prefix that existed when the first segment returned is captured.

The proxy returns `null` when no SQL query window is the active document; the reader surfaces this as `MessagesCaptureStatus.NoActiveWindow`. Per-status messaging is rendered into the WPF control via `StatisticsParserControl.ShowCaptureError(...)`.

**Version-drift recovery**: every brokered type/method is looked up by name through `ContractTypes.GetAsync(...)`, which throws a descriptive `InvalidOperationException` when a name has moved. SSMS minor-version bumps surface immediately at the diagnostics pane rather than as silent failures.

### 4. Parser Engine (`StatisticsParser.Core`)

The C# parser is a port of the JavaScript parser at [github.com/Jorriss/StatisticsParser — parser.js](https://github.com/Jorriss/StatisticsParser/blob/master/src/assets/js/modules/parser.js). It processes a raw string in a single stateful pass and returns a `ParseResult`.

#### Enums

```csharp
enum RowType
{
    None, IO, ExecutionTime, CompileTime, RowsAffected,
    Error, IOTotal, ExecutionTimeTotal, CompileTimeTotal,
    Info, CompletionTime
}

enum IoColumn
{
    NotFound, Table, Scan, Logical, Physical, PageServer,
    ReadAhead, PageServerReadAhead, LobLogical, LobPhysical,
    LobPageServer, LobReadAhead, LobPageServerReadAhead,
    PercentRead, SegmentReads, SegmentSkipped
}
```

#### Models

All result row types implement `IResultRow` (exposing `RowType RowType`):

| C# Class | JS Equivalent | Purpose |
|---|---|---|
| `IoRow` | `StatsIOInfo` | One table's IO stats |
| `IoGroupTotal` | `StatsIOInfoTotal` | Totals row for an IO group |
| `TimeRow` | `StatsTimeInfo` | One compile or execution time entry |
| `TimeTotal` | `StatsTimeInfoTotal` | Running time accumulator |
| `RowsAffectedRow` | `RowsAffectedInfo` | "N rows affected" line |
| `ErrorRow` | `ErrorInfo` | SQL Server error message |
| `InfoRow` | `TextInfo` | Unrecognized / passthrough text |
| `CompletionTimeRow` | `CompletionTimeInfo` | Completion timestamp |

IO rows are grouped into `IoGroup` objects (one per consecutive block of table lines):

```
IoGroup : IResultRow
├── TableId: string           // unique ID, e.g. "resultTable_0"
├── Columns: List<IoColumn>   // columns present in this group
├── Data: List<IoRow>         // one entry per table line
└── Total: IoGroupTotal       // sum of all numeric columns in this group
```

#### Output

```
ParseResult
├── Data: List<IResultRow>    // all parsed elements in input order;
│                             // IoGroup entries appear inline where their
│                             // block of Table lines appeared
├── TableCount: int
└── Total
    ├── ExecutionTotal: TimeTotal
    ├── CompileTotal: TimeTotal
    └── IoTotal
        ├── Columns: List<IoColumn>       // union of columns across all groups
        ├── Data: List<IoGroupTotal>      // one entry per unique table name
        │                                 // across all statements, sorted by name
        └── Total: IoGroupTotal           // sum of Data
```

#### Language Support

`ParseData(string text, ParserLanguage lang)` accepts a `ParserLanguage` instance that maps SQL Server output text to the parser's internal values, mirroring the JS `lang` parameter. `StatisticsParser.Core` ships three static singletons:

- `ParserLanguage.English` — used as the default by `ParseData` when callers don't supply one
- `ParserLanguage.Spanish`
- `ParserLanguage.Italian`

Values are hardcoded in C#, sourced from the three upstream JSON files in [Jorriss/StatisticsParser](https://github.com/Jorriss/StatisticsParser/tree/master/src/public/data) — [languagetext-en.json](https://github.com/Jorriss/StatisticsParser/blob/master/src/public/data/languagetext-en.json), [languagetext-es.json](https://github.com/Jorriss/StatisticsParser/blob/master/src/public/data/languagetext-es.json), [languagetext-it.json](https://github.com/Jorriss/StatisticsParser/blob/master/src/public/data/languagetext-it.json). Maintenance path: re-pull from upstream when language data changes. No JSON parser ships in `StatisticsParser.Core`, preserving its zero-dependency rule.

Identification fields:

| C# property | Type | Values | Purpose |
|---|---|---|---|
| `LangValue` | `string` | `"en"` / `"es"` / `"it"` | BCP-47-style code; matches upstream `langvalue` |
| `LangName` | `string` | `"English"` / `"Español"` / `"Italian"` | Display name; matches upstream `langname` (Italian quirk: upstream uses the English word) |

Parser-relevant fields:

| C# property | Type | English value | Purpose |
|---|---|---|---|
| `Table` | `string` | `"Table"` | IO line prefix; also Table column name |
| `ExecutionTime` | `string` | `"SQL Server Execution Times:"` | Header line equality match |
| `CompileTime` | `string` | `"SQL Server parse and compile time:"` | Header line equality match |
| `CpuTime` | `string` | `"CPU time = "` | Substring in time data line (note trailing space) |
| `ElapsedTime` | `string` | `"elapsed time = "` | Substring in time data line |
| `Milliseconds` | `string` | `"ms"` | Unit token after numeric values |
| `RowsAffected` | `IReadOnlyList<string>` | `["row(s) affected", "row affected", "rows affected"]` | Any substring match indicates a rows-affected line |
| `ErrorMsg` | `string` | `"Msg"` | First-3-chars match of error lines (Italian: `"Mes"`) |
| `CompletionTimeLabel` | `string` | `"Completion time: "` | Line prefix; same in all three languages (trailing space) |
| `Scan` | `IReadOnlyList<string>` | `["scan count"]` | Column-name variants for `IoColumn.Scan` |
| `Logical` | `IReadOnlyList<string>` | `["logical reads"]` | `IoColumn.Logical` |
| `Physical` | `IReadOnlyList<string>` | `["physical reads"]` | `IoColumn.Physical` |
| `PageServer` | `IReadOnlyList<string>` | `["page server reads"]` | `IoColumn.PageServer` |
| `ReadAhead` | `IReadOnlyList<string>` | `["read-ahead reads"]` | `IoColumn.ReadAhead` |
| `PageServerReadAhead` | `IReadOnlyList<string>` | `["page server read-ahead reads"]` | `IoColumn.PageServerReadAhead` |
| `LobLogical` | `IReadOnlyList<string>` | `["lob logical reads"]` | `IoColumn.LobLogical` |
| `LobPhysical` | `IReadOnlyList<string>` | `["lob physical reads"]` | `IoColumn.LobPhysical` |
| `LobPageServer` | `IReadOnlyList<string>` | `["lob page server reads"]` | `IoColumn.LobPageServer` |
| `LobReadAhead` | `IReadOnlyList<string>` | `["lob read-ahead reads"]` | `IoColumn.LobReadAhead` |
| `LobPageServerReadAhead` | `IReadOnlyList<string>` | `["lob page server read-ahead reads"]` | `IoColumn.LobPageServerReadAhead` |
| `SegmentReads` | `IReadOnlyList<string>` | `["segment reads"]` | `IoColumn.SegmentReads` |
| `SegmentSkipped` | `IReadOnlyList<string>` | `["segment skipped"]` | `IoColumn.SegmentSkipped` |

Non-English equivalents (Spanish `Tabla`, Italian `Tabella`, etc.) are sourced verbatim from the upstream JSON. Spanish includes synonym phrases for several columns — e.g., `loblogical: ["lecturas lógicas de lob", "lecturas lógicas de línea de negocio"]` — which is why every column-variant field is `IReadOnlyList<string>`.

**Method**: `IoColumn DetermineIoColumn(string columnText)` matches `columnText.Trim()` case-insensitively against each column-variant list and returns the matching `IoColumn` enum value, or `IoColumn.NotFound` if no list contains the input. Mirrors the JS `getIOColumnEnum` pattern.

**Deferred** (tracked in [TODO.md](TODO.md)):

- Language **selection / auto-detect** — `ParseData` accepts a `ParserLanguage` but the SSMS extension always passes `English`. Adding either auto-detect from input markers or a Tools > Options page is future work.
- **Locale-specific number formats** — Spanish and Italian SQL Server output may emit numbers using `.` thousand separator and `,` decimal separator (per the upstream `numberformat` block). The current parser assumes invariant integers.
- **UI display string localization** — the Stats Parser tool window renders English column headers (`"Logical Reads"`, `"Totals:"`, `"rows affected"`, etc.) regardless of the parsed input language. Header fields from the upstream JSON (`headerscan`, `headerlogical`, `totals`, `headerrowsaffected`, etc.) are intentionally **not** part of `ParserLanguage` at this stage.

#### Algorithm

`ParseData` splits the input on newlines and iterates line by line, maintaining:

- `prevRowType` — type of the previous line; used to detect IO block boundaries
- `currentGroup` — the active `IoGroup` being built
- `executionTotal` / `compileTotal` — running `TimeTotal` accumulators
- `tableIoGrandTotal` — `List<IoGroupTotal>` accumulating cross-statement totals by table name
- `ioColumns` / `ioTotalColumns` — column presence trackers

**Row type detection** (`DetermineRowType`):

| Condition | RowType |
|---|---|
| Line starts with `lang.Table` | `IO` |
| Line equals `lang.ExecutionTime` | `ExecutionTime` |
| Line equals `lang.CompileTime` | `CompileTime` |
| Line contains any `lang.RowsAffected` string | `RowsAffected` |
| Line starts with `lang.ErrorMsg` (3 chars) | `Error` |
| Line starts with `lang.CompletionTimeLabel` | `CompletionTime` |
| Otherwise | `None` |

**IO block handling**: IO rows form groups by consecutive occurrence. When a non-IO row follows one or more IO rows, the current group is finalized: per-group totals are computed, `% Logical Reads` (to 3 decimal places) is calculated for each row, and `IoColumn.PercentRead` is appended to the column list. The `IoGroup` is already in `Data`; only its properties are filled in at close time.

**Segment reads special case**: If a parsed IO line yields `SegmentReads > 0` or `SegmentSkipped > 0`, those values are merged into the last `IoRow` already in the current group rather than appending a new row. SQL Server outputs segment statistics as a continuation line for the same table.

**Time parsing (two-line look-ahead)**: When a `CompileTime` or `ExecutionTime` header is detected, the iterator advances by one to read the actual data line containing `CPU time = N ms, elapsed time = N ms`. CPU and elapsed are stored as raw `int` milliseconds. For `ExecutionTime` rows, `DetermineSummaryRow` checks whether the values match the sum of all prior execution and compile totals (within ±5 ms on elapsed). If so, `TimeRow.Summary = true` and the row is excluded from `executionTotal` to prevent double-counting.

**Error parsing (two-line capture)**: An `Error` row causes two `ErrorRow` objects to be appended to `Data` — the current line and `lines[i+1]`. The iterator advances past the second line. SQL Server errors span two output lines.

**Grand total IO**: After each IO row is appended to the current group it is also merged into `tableIoGrandTotal` via `ProcessGrandTotal`, which accumulates numeric columns by table name. At the end of the parse, `% Logical Reads` is recomputed against the grand total and the list is sorted alphabetically by table name.

### 5. In-Pane Results Tab

The Stats Parser output is rendered as a third tab — labeled **"Parse Statistics"** — sitting inside the SSMS query window's `SqlScriptEditorControl` Results/Messages tab strip, alongside the native Results and Messages tabs. There is no separate dockable tool window.

This is achieved by reflecting into `SqlScriptEditorControl`'s WinForms `TabPageHost` (a `DisplaySqlResultsTabControl` that derives from or contains a `System.Windows.Forms.TabControl`) and calling its public `TabPages.Add(...)` to insert a new `TabPage`. The TabPage hosts a WinForms-Forms-Integration `ElementHost` whose `Child` is our WPF `StatisticsParserControl`. SSMS code paths and the WinForms TabControl API are stable, public, and well-documented; the only fragile step is the reflection on the `TabPageHost` property name itself. Both `SqlScriptEditorControl.TabPageHost` (public property) and the private `tabPagesHost` field are tried as fallbacks.

#### Components

- [InPaneTab/ResultsTabInjector.cs](../source/StatisticsParser.Vsix/InPaneTab/ResultsTabInjector.cs) — thin entry point. Resolves the active query window's docView via `IVsMonitorSelection.SEID_DocumentFrame → __VSFPROPID.VSFPROPID_DocView`, then delegates to a per-docView `TabPageSupervisor`.
- [InPaneTab/TabPageSupervisor.cs](../source/StatisticsParser.Vsix/InPaneTab/TabPageSupervisor.cs) — one instance per `SqlScriptEditorControl`. Owns the `TabPage` and the embedded `StatisticsParserControl`. Stored in a `ConditionalWeakTable<object, TabPageSupervisor>` keyed on the docView so the supervisor (and its event subscriptions) get garbage-collected together with the docView when the query window closes — no explicit unhook needed.

#### Lifecycle

1. **First invocation** (right-click → Parse Statistics): the supervisor is created, the `TabPage` is added, the WPF control is filled in, and the new tab is selected. The supervisor reflects across `SqlScriptEditorControl` and its `m_sqlResultsControl` (a `DisplaySQLResultsControl`) for events whose names match the heuristic pattern `Completed | Executed | Finished | Stopped | Done` and subscribes to all matches via dynamically-built delegates (`Expression.Lambda(delegateType, ...)` adapts arbitrary event signatures).
2. **Subsequent query executions** (F5): SSMS clears its tab strip on query start, so when our hooked completion event fires, the supervisor (a) re-resolves `TabPageHost` via reflection (in case SSMS replaced the tab control instance), (b) re-creates the `TabPage` if it's missing, (c) re-captures Messages text via the brokered service, (d) re-renders the WPF control. Selection is left at whatever SSMS chose — F5 naturally focuses Results, which is the SSMS-default UX we don't fight.
3. **Query window closes**: docView is disposed, supervisor and event subscriptions are collected.

#### WPF rendering inside the TabPage

The WPF `StatisticsParserControl` ([Controls/StatisticsParserControl.xaml](../source/StatisticsParser.Vsix/Controls/StatisticsParserControl.xaml)) currently shows a minimum-viable display: a status line ("Captured N chars; parsed N rows") plus a read-only monospace `TextBox` containing the captured Messages text. Phase 9 (deferred) replaces this with structured rendering of `ParseResult`:

- **Rows affected**: Bold text label (`100 rows affected`).
- **IO Statistics table**: WPF `DataGrid` — one row per table, plus a pinned Total row at the bottom. Only columns present in `IoGroup.Columns` are shown.
- **Execution time table**: WPF `DataGrid` with CPU and Elapsed columns; values formatted as `hh:mm:ss.ms`. Rows with `TimeRow.Summary = true` are not rendered as additive entries.
- **Error messages**: Highlighted text.
- **Completion time**: Plain text label, timestamp formatted in the local culture.
- **Totals section** (after all statements): Grand IO total `DataGrid` and grand time total `DataGrid`.

The WPF control should respect the active SSMS color theme by binding to VS resource keys (Phase 10). Theming flow across the `ElementHost` boundary is preserved by ambient property inheritance.

**Sorting**: Deferred — see [TODO.md](TODO.md).

## Build & Packaging

A single VSIX targets SSMS 22 (64-bit, VS 2026 shell). CI produces one release artifact:

| Artifact | Target |
|---|---|
| `StatisticsParser.vsix` | SSMS 22 |

Build configurations:
- `Debug | AnyCPU` → VSIX project (development)
- `Release | AnyCPU` → VSIX project (release artifact)

Both `StatisticsParser.Core` and `StatisticsParser.Vsix` build as `AnyCPU`. The VSIX runs in the 64-bit SSMS / VS host process; install eligibility is enforced via `<ProductArchitecture>amd64</ProductArchitecture>` in [source.extension.vsixmanifest](../source/StatisticsParser.Vsix/source.extension.vsixmanifest), not the assembly platform. `StatisticsParser.Core`'s .NET Standard 2.0 output is referenced by both the VSIX project (at runtime on .NET Framework 4.8) and the test project (at runtime on .NET 8).

## Distribution

Releases are published to **GitHub Releases**. Each release includes:
- `StatisticsParser.vsix`
- Release notes describing changes

Installation: users download `StatisticsParser.vsix` and double-click to install via the VSIX installer. SSMS must be closed during installation.

See [TODO.md](TODO.md) for planned future distribution channels.
