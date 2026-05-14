# Changelog

## 1.0.0 — 2026-05-14

First public release. SSMS 22 extension that parses `STATISTICS IO` / `STATISTICS TIME` output from the Messages tab and renders it as a third tab — **Parse Statistics** — inside the query window.

### Parsing

- C# port of [Jorriss/StatisticsParser](https://github.com/Jorriss/StatisticsParser).
- Per-statement `STATISTICS IO` tables: Scan count, Logical Reads, Physical Reads, Read-ahead Reads, LOB Logical/Physical/Read-ahead Reads, plus a `% Logical Reads` share column.
- Per-statement `STATISTICS TIME` rows formatted as `hh:mm:ss.ms` for both CPU time and elapsed time.
- Cross-statement **Totals** section: grand IO total per table across the batch and grand CPU/elapsed totals.
- Rows-affected lines, error messages (in red), and statement completion timestamps surfaced inline.

### Surface in SSMS

- Right-click **Parse Statistics** on the query body, or press `Ctrl+K, Ctrl+G`.
- Output lands in a **Parse Statistics** tab next to the native Results and Messages tabs in the same query window.
- Subsequent executions in the same window auto-refresh the tab — no extra click needed.

### Rendering

- Structured WPF tables with sortable columns, right-aligned numeric headers, bold totals, and selectable text.
- Long temp-table names truncate with a tooltip showing the full name.
- **Copy all** command for pasting the parsed view elsewhere.
- Respects all SSMS 22 themes (light, dark, blue, and the new SSMS 22 themes such as Bubblegum).

### Tools > Options (Statistics Parser)

- Font size for the Parse Statistics tab.
- Hide all-zero columns to reduce clutter.
- Temp-table name handling: **Query names** (default, strips the per-session suffix so `#temp_______…` collapses to `#temp`), **Shorten names** (collapses long underscore runs to `…`), or **Do not change names**.
- Convert Completion Time to local time, so `Completion Time` lines from the Messages tab show in the local time zone instead of the server's reported offset.

### Other

- About dialog with version, credit to Jorriss's original project, and Brent Ozar Unlimited branding.
- Icons for the menu command and context menu entry.

### Compatibility

- Supports SSMS 22.0 through 22.6+. SSMS 22.6 rebuilt `BrokeredContracts.dll` and changed the signature of `IQueryEditorTabDataServiceBrokered` methods; brokered-method reflection now adapts to both pre- and post-22.6 contracts so Parse Statistics works on every SSMS 22 build.

### Requirements

- SSMS 22 (64-bit, VS 2026 shell)
- .NET Framework 4.8 (ships with SSMS 22)

### Install

See [README.md](README.md#install) for install and uninstall steps. Note that double-clicking the `.vsix` may not work on SSMS 22 — you'll may need the SSMS-bundled `VSIXInstaller.exe`.

### Known limitations

- The parser ships with English, Spanish, and Italian language tables, but the UI currently always invokes the English parser. Language auto-detect and a Tools > Options selector are tracked in [docs/TODO.md](docs/TODO.md).
- Column headers in the rendered tab are English only, even when the parsed input uses Spanish or Italian markers.

### Credits

Parsing logic ported from Jorriss's [StatisticsParser](https://github.com/Jorriss/StatisticsParser).
