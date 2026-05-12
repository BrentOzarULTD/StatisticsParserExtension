# Changelog

## 1.0 — 2026-05-12

First public release. SSMS 22 extension that parses `STATISTICS IO` / `STATISTICS TIME` output from the Messages tab and renders it as a third tab — **Parse Statistics** — inside the query window.

### Parsing

- C# port of [Jorriss/StatisticsParser](https://github.com/Jorriss/StatisticsParser), with `parser.js` as the authoritative reference.
- Per-statement `STATISTICS IO` tables: Scan count, Logical Reads, Physical Reads, Read-ahead Reads, LOB Logical/Physical/Read-ahead Reads, plus a `% Logical Reads` share column.
- Per-statement `STATISTICS TIME` rows formatted as `hh:mm:ss.ms` for both CPU time and elapsed time.
- Cross-statement **Totals** section: grand IO total per table across the batch and grand CPU/elapsed totals.
- Rows-affected lines, error messages, and statement completion timestamps surfaced inline.

### Surface in SSMS

- Right-click **Parse Statistics** on the query body, or press `Ctrl+K, Ctrl+G`.
- Output lands in a **Parse Statistics** tab next to the native Results and Messages tabs in the same query window.
- Subsequent executions in the same window auto-refresh the tab — no extra click needed.

### Rendering

- Structured WPF tables with sortable columns, right-aligned numeric headers, bold totals, and selectable text.
- Long temp-table names truncate with a tooltip showing the full name.
- **Copy all output** command for pasting the parsed view elsewhere.
- Respects SSMS light, dark, and blue themes.

### Tools > Options (Statistics Parser)

- Font size for the Parse Statistics tab.
- Hide all-zero columns to reduce clutter.
- Query-name temp-table mode strips the per-session suffix so `#temp_______…` collapses to `#temp` across executions.

### Other

- About dialog with version, credit to Jorriss's original project, and Brent Ozar Unlimited branding.
- Icons for the menu command and context menu entry.

### Requirements

- SSMS 22 (64-bit, VS 2026 shell)
- .NET Framework 4.8 (ships with SSMS 22)

### Install

Download `StatisticsParser.vsix` from the GitHub release, close SSMS, double-click the `.vsix`, and relaunch SSMS. See [README.md](README.md) for full install and uninstall steps.

### Known limitations

- The parser ships with English, Spanish, and Italian language tables, but the UI currently always invokes the English parser. Language auto-detect and a Tools > Options selector are tracked in [docs/TODO.md](docs/TODO.md).
- Column headers in the rendered tab are English only, even when the parsed input uses Spanish or Italian markers.

### Credits

Parsing logic ported from Jorriss's [StatisticsParser](https://github.com/Jorriss/StatisticsParser).
