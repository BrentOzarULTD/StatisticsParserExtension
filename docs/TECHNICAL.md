# Statistics Parser SSMS Extension — Technical Requirements

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
        ├── Commands/             # Context menu command handlers
        ├── Windows/              # Tool window (ToolWindowPane subclass)
        ├── Controls/             # WPF UserControls (results view)
        └── source.extension.vsixmanifest
```

`StatisticsParser.Core` targets .NET Standard 2.0, which is compatible with both the .NET Framework 4.8 VSIX host and the .NET 8 test project. All parsing and data model logic lives here; the VSIX project contains only SSMS integration code and WPF UI.

## Extension Architecture

SSMS extensions are MEF-based VSIX packages built on the Visual Studio Isolated Shell. The entry point is a `Package` class (subclass of `AsyncPackage` or `Package`) registered via attributes. SSMS loads the package on demand or at IDE startup depending on the `ProvideAutoLoad` rule set.

Key MEF/VS concepts used:
- **VSPackage**: The root package class, handles initialization and service registration.
- **.vsct file**: Defines commands (menu items, toolbar buttons) and their placement within command groups. The context menu item on the Messages tab is declared here.
- **ToolWindowPane**: Base class for custom dockable/tabbed windows inside SSMS.
- **IVsOutputWindowPane**: VS interface used to read content from the Messages tab.

## Key Components

### 1. SSMS Package Registration

The `Package` class bootstraps the extension:
- Registers the context menu command in the Messages tab right-click menu.
- Registers the Stats Parser tool window with SSMS.
- Acquires references to SSMS services needed at runtime (`IVsOutputWindow`, `DTE2`, etc.).

The `.vsct` file must declare the command group GUID and command ID for placement in the Messages tab context menu. The exact GUID for the Messages tab context menu requires verification against the SSMS SDK or reverse engineering of the SSMS command table — this is a discovery task during initial development.

### 2. Context Menu Integration

A single command — **"Parse Statistics"** — appears in the right-click context menu of the Messages tab.

Behavior on invocation:
1. Capture the full text content of the Messages tab (see §3).
2. Pass the text to the Core parser.
3. Open (or activate if already open) the Stats Parser tool window.
4. Render the parsed results in the tool window.

If the Messages tab is empty or contains no recognizable statistics output, the tool window should display an appropriate empty state message.

### 3. Messages Tab Content Capture

The Messages tab in SSMS is exposed as an `IVsOutputWindowPane`. To read its content:

1. Obtain the `IVsOutputWindow` service via `GetService(typeof(SVsOutputWindow))`.
2. Look up the Messages tab pane by its well-known GUID (the SSMS Messages pane GUID — requires verification against SSMS SDK documentation).
3. Call `IVsOutputWindowPane.GetText()` to retrieve the full text as a string.

This text is then handed directly to the parser. No user copy/paste is required.

**Risk**: `IVsOutputWindowPane.GetText()` availability depends on SSMS exposing the Messages pane via the standard `IVsOutputWindow` interface. If SSMS uses a custom pane implementation that does not implement `GetText()`, an alternative capture strategy will be needed (e.g., hooking into query execution completion events and accumulating output there). This must be validated early in development.

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

The parser's entry point is `ParseData(string text, ParserLanguage lang)`. The `ParserLanguage` object maps SQL Server output text to the parser's internal values, mirroring the JS `lang` parameter. This allows the parser to handle SQL Server output in any locale. An English default (`ParserLanguage.English`) is provided.

Key `ParserLanguage` fields:

| Field | English value | Purpose |
|---|---|---|
| `Table` | `"Table"` | IO line prefix; also column name |
| `ExecutionTime` | `"SQL Server Execution Times:"` | Header that precedes execution time data |
| `CompileTime` | `"SQL Server parse and compile time:"` | Header that precedes compile time data |
| `RowsAffected` | `["rows affected", "row affected"]` | Substrings matched in rows-affected lines |
| `ErrorMsg` | `"Msg"` | First 3 chars of error lines |
| `CompletionTimeLabel` | `"Completion time:"` | Prefix of completion time lines |
| `CpuTime` | `"CPU time ="` | Token in time data lines |
| `ElapsedTime` | `"elapsed time ="` | Token in time data lines |
| `Milliseconds` | `"ms"` | Token in time data lines |
| `Scan`, `Logical`, `Physical`, ... | column name variants | Used to map IO column names to `IoColumn` enum |

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

### 5. Stats Parser Tool Window (WPF)

The Stats Parser output is shown in a dockable SSMS tool window (not a document tab). The window can be docked, floated, or auto-hidden like any standard SSMS window.

The window contains a WPF `ScrollViewer` hosting a `StackPanel` of result sections rendered top-to-bottom in the order they appear in the input:

- **Rows affected**: Bold text label (`100 rows affected`).
- **IO Statistics table**: WPF `DataGrid` — one row per table, plus a pinned Total row at the bottom. Only columns present in the `IoGroup.Columns` list are shown.
- **Execution time table**: Small WPF `DataGrid` with CPU and Elapsed columns. CPU and elapsed values are stored by the parser as raw milliseconds (`int`); the UI formats them as `hh:mm:ss.ms` (e.g. `959 ms` → `00:00:00.959`). Rows with `TimeRow.Summary = true` are not rendered as additive entries.
- **Error messages**: Highlighted text (e.g. red/amber depending on severity).
- **Completion time**: Plain text label, timestamp formatted in the local culture.
- **Totals section** (after all statements): Grand IO total `DataGrid` and grand time total `DataGrid`.

The tool window respects the active SSMS color theme (light/dark/blue) by binding to VS resource keys rather than hardcoded colors.

**Sorting**: Deferred — see [TODO.md](TODO.md).

## Build & Packaging

A single VSIX targets SSMS 22 (64-bit, VS 2026 shell). CI produces one release artifact:

| Artifact | Target |
|---|---|
| `StatisticsParser.vsix` | SSMS 22 |

Build configurations:
- `Debug | x64` → VSIX project (development)
- `Release | x64` → VSIX project (release artifact)

`StatisticsParser.Core` is `AnyCPU`. Its .NET Standard 2.0 output is referenced by both the VSIX project (at runtime on .NET Framework 4.8) and the test project (at runtime on .NET 8).

## Distribution

Releases are published to **GitHub Releases**. Each release includes:
- `StatisticsParser.vsix`
- Release notes describing changes

Installation: users download `StatisticsParser.vsix` and double-click to install via the VSIX installer. SSMS must be closed during installation.

See [TODO.md](TODO.md) for planned future distribution channels.
