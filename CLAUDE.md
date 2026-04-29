# CLAUDE.md

SSMS 22 extension that parses `STATISTICS IO` / `STATISTICS TIME` output from the Messages tab and renders it in a dockable WPF tool window. C# port of [Jorriss/StatisticsParser](https://github.com/Jorriss/StatisticsParser) (parser.js is authoritative for parsing logic).

## Read first

- [docs/PLAN.md](docs/PLAN.md) — phased implementation plan; the source of truth for what to build next
- [docs/TECHNICAL.md](docs/TECHNICAL.md) — architecture, models, parser algorithm, target environments
- [docs/FUNCTIONAL.md](docs/FUNCTIONAL.md) — user-facing behavior with input/output examples
- [docs/TODO.md](docs/TODO.md) — deferred work; do not modify unless the user asks

## Projects

- [source/StatisticsParser.Core/](source/StatisticsParser.Core/) — `netstandard2.0`, parsing + models, no external deps
- [source/StatisticsParser.Core.Tests/](source/StatisticsParser.Core.Tests/) — `net8.0`, xUnit
- [source/StatisticsParser.Vsix/](source/StatisticsParser.Vsix/) — `net48` x64, WPF + VSIX, references Core

## Build & test

```
dotnet build StatisticsParserExtension.sln
dotnet test source/StatisticsParser.Core.Tests
msbuild source/StatisticsParser.Vsix /p:Configuration=Release /p:Platform=x64
```

After any change to `StatisticsParser.Core`, always run `dotnet test source/StatisticsParser.Core.Tests` before reporting the task done.

The VSIX project (Phases 7–11 in PLAN.md) requires Windows + VS 2026 + SSMS 22. Phases 1–6 (Core + Tests) must be green before starting Phase 7.

## Workflow

On Windows, use the PowerShell tool, not Bash. Claude Code's Bash launcher fails with `Exit code 5` (ERROR_ACCESS_DENIED) on this repo because both the working directory and the Git Bash install path contain spaces (Claude Code issues #4507, #20551). PowerShell has no such issue.

