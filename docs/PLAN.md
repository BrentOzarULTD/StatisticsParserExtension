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
| 4 | SSMS 22 NuGet SDK packages | Discovery task — research spike at start of Phase 7 |
| 5 | Messages tab context menu GUID | Discovery task — inspect SSMS command table at start of Phase 7 |

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

## Phase 6 — Unit Tests

`source/StatisticsParser.Core.Tests/Parsing/ParserTests.cs`

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

## Phase 7 — VSIX Package Setup *(Windows only)*

### Research spike (before coding)
1. Identify correct NuGet package IDs for SSMS 22 SDK (candidates: `Microsoft.VisualStudio.SDK`, `Community.VisualStudio.Toolkit`, SSMS-specific overlays).
2. Discover Messages tab context menu GUID via SSMS SDK docs, devenv command table inspection, or SSMS extension samples.

### Files to create
- `source/StatisticsParser.Vsix/source.extension.vsixmanifest` — identity, version, VS 2026 shell prerequisite
- `source/StatisticsParser.Vsix/StatisticsParser.vsct` — command group GUID + command ID; placement in Messages tab context menu
- `source/StatisticsParser.Vsix/Commands/StatisticsParserPackage.cs` — `AsyncPackage` subclass with `[PackageRegistration]`, `[ProvideMenuResource]`, `[ProvideToolWindow]` attributes; `InitializeAsync` registers command and tool window
- `source/StatisticsParser.Vsix/Commands/ParseStatisticsCommand.cs` — `Execute` handler: capture → parse → show window

**Verification**: VSIX loads in SSMS 22 experimental instance; "Parse Statistics" appears in Messages tab right-click menu.

---

## Phase 8 — Messages Tab Content Capture

`source/StatisticsParser.Vsix/Commands/MessagesTabReader.cs`

```csharp
public static string GetMessagesText(IServiceProvider serviceProvider)
```

Steps:
1. `GetService(typeof(SVsOutputWindow))` → `IVsOutputWindow`
2. Look up Messages pane by its well-known GUID
3. `IVsOutputWindowPane.GetText(out string text)`

**Risk**: If `GetText()` is unavailable on the SSMS Messages pane, fall back to subscribing to query-execution completion events and accumulating output there. Validate `GetText()` availability in the experimental instance before building the fallback.

**Verification**: Captured text matches Messages tab content (manual smoke test in SSMS).

---

## Phase 9 — Tool Window & WPF UI

**Shell**
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

## Remaining Risks

| Risk | Mitigation |
|---|---|
| SSMS 22 NuGet SDK package IDs unknown | Research spike at start of Phase 7 |
| Messages tab context menu GUID unknown | Discovery step at start of Phase 7 |
| `IVsOutputWindowPane.GetText()` may be unavailable | Validate early in Phase 8; fallback to query-execution event subscription |

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
    Parsing/
      ParserTests.cs
  StatisticsParser.Vsix/
    StatisticsParser.Vsix.csproj
    source.extension.vsixmanifest
    StatisticsParser.vsct
    Commands/
      StatisticsParserPackage.cs
      ParseStatisticsCommand.cs
      MessagesTabReader.cs
    Windows/
      StatisticsParserToolWindow.cs
    Controls/
      StatisticsParserControl.xaml
      StatisticsParserControl.xaml.cs
.github/workflows/build.yml
```
