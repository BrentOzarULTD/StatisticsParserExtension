# Statistics Parser SSMS Extension

An SSMS 22 extension that parses `STATISTICS IO` / `STATISTICS TIME` output from the Messages tab and renders it as a sortable, readable third tab — **Parse Statistics** — alongside the native Results and Messages tabs.

C# port of [Jorriss/StatisticsParser](https://github.com/Jorriss/StatisticsParser), brought into the query window so you never have to copy/paste output into a separate web tool again.

## Features

- One-click parsing via right-click in the query window or `Ctrl+K, Ctrl+G`
- Per-statement IO tables with table totals and `% Logical Reads` share
- CPU / Elapsed time tables formatted as `hh:mm:ss.ms`
- Cross-statement **Totals** section: grand IO total per table + grand time total
- Auto-refresh on subsequent query executions (F5)
- Respects SSMS light / dark / blue themes
- Rows-affected, error messages (red), and completion timestamps surfaced inline

See [docs/FUNCTIONAL.md](docs/FUNCTIONAL.md) for full input/output examples.

## Requirements

| Component | Version |
|---|---|
| SSMS | 22 (64-bit, VS 2026 shell) |
| .NET Framework | 4.8 (ships with SSMS 22) |

## Install

1. Download `StatisticsParser.vsix` from [Releases](https://github.com/BrentOzarULTD/StatisticsParserExtension/releases).
2. Close SSMS.
3. Run SSMS 22's bundled VSIX installer against the downloaded file. Change the directory to the path where you downloaded StatisticsParser.vsix:

   ```powershell
   & "C:\Program Files\Microsoft SQL Server Management Studio 22\Release\Common7\IDE\VSIXInstaller.exe" StatisticsParser.vsix
   ```

   Adjust the `.vsix` path if you saved it somewhere other than `Downloads`. Confirm the prompt in the VSIX Installer dialog.
4. Launch SSMS.

Double-clicking the `.vsix` does **not** work on most machines: Windows associates the file with Visual Studio's installer (or shows a "Select an app" picker), and neither route knows how to install into SSMS 22.

## Use

1. Run a query with `SET STATISTICS IO, TIME ON;`.
2. Right-click anywhere in the query body and choose **Parse Statistics** (or press `Ctrl+K, Ctrl+G`).
3. The **Parse Statistics** tab appears next to the Messages tab.

Subsequent executions in the same query window auto-refresh the tab.

## Uninstall

SSMS 22 does not include a Manage Extensions dialog, so the extension must be removed from the command line. Close SSMS, then run:

```powershell
& "C:\Program Files\Microsoft SQL Server Management Studio 22\Release\Common7\IDE\VSIXInstaller.exe" /uninstall:StatisticsParser.4A9EFF2E-819B-453D-BE4C-5DF7B343C0E7
```

Confirm the prompt in the VSIX Installer dialog, then reopen SSMS.

## Build from source

```powershell
dotnet build StatisticsParserExtension.sln
dotnet test source\StatisticsParser.Core.Tests
msbuild source\StatisticsParser.Vsix /p:Configuration=Release /p:Platform=x64
```

The VSIX output lands at `source\StatisticsParser.Vsix\bin\x64\Release\StatisticsParser.vsix`.

## Projects

- [source/StatisticsParser.Core/](source/StatisticsParser.Core/) — `netstandard2.0` parser and models, no external dependencies
- [source/StatisticsParser.Core.Tests/](source/StatisticsParser.Core.Tests/) — xUnit tests on `net8.0`
- [source/StatisticsParser.Vsix/](source/StatisticsParser.Vsix/) — `net48` VSIX with WPF UI and SSMS integration

## Docs

- [docs/PLAN.md](docs/PLAN.md) — phased implementation plan
- [docs/TECHNICAL.md](docs/TECHNICAL.md) — architecture, parser algorithm, target environments
- [docs/FUNCTIONAL.md](docs/FUNCTIONAL.md) — user-facing behavior with examples
- [docs/HOWTOTEST.md](docs/HOWTOTEST.md) — manual test procedure

## Credits

Parsing logic ported from Jorriss's [StatisticsParser](https://github.com/Jorriss/StatisticsParser) (`parser.js` is authoritative).
