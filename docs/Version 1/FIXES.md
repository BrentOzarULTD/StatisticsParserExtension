# Fixes

Ad-hoc developer-environment fixes that don't belong in [PLAN.md](PLAN.md) or [TECHNICAL.md](TECHNICAL.md). Each entry is a self-contained symptom → cause → fix.

---

## VS IntelliSense reports "Package was not found" after a POSIX-shell restore

### Symptom

Visual Studio's Error List shows two NuGet errors followed by a long cascade of namespace errors in the [Vsix](../source/StatisticsParser.Vsix/) project. Representative entries:

```
Package xunit.analyzers, version 1.16.0 was not found. It might have been
deleted since NuGet restore. Otherwise, NuGet restore might have only
partially completed, which might have been due to maximum path length
restrictions.

Package Community.VisualStudio.Toolkit.Analyzers, version 1.0.533 was not
found. ...

The type or namespace name 'Community' could not be found ...
The type or namespace name 'VisualStudio' does not exist in the namespace 'Microsoft' ...
The type or namespace name 'BaseCommand<>' could not be found ...
The type or namespace name 'OleMenuCmdEventArgs' could not be found ...
The name 'Package' does not exist in the current context
The name 'ThreadHelper' does not exist in the current context
The name 'ErrorHandler' does not exist in the current context
'StatisticsParserPackage' does not contain a definition for 'RegisterCommandsAsync' ...
```

The build may also fail with similar errors.

### Root cause

The error message is misleading. The packages are present in `C:\Users\richie\.nuget\packages\`. The actual problem is in `obj/project.assets.json` for each of the three projects (Core, Core.Tests, Vsix):

```json
"packageFolders": { "/Users/richie/.nuget/packages/": {} }
"project": { "restore": { "packagesPath": "/Users/richie/.nuget/packages/" } }
```

That's a POSIX-style path. On Windows the cache lives at `C:\Users\richie\.nuget\packages\`, so MSBuild and VS look at the literal path `/Users/richie/.nuget/packages/`, find nothing, and emit "Package was not found." Every cascading namespace error in `Commands\StatisticsParserPackage.cs`, `Commands\ParseStatisticsCommand.cs`, etc. flows from that one bad path.

The bad assets file was written by a previous `dotnet restore` invocation that ran in a POSIX shell (Git Bash / WSL / a container) with `HOME=/Users/richie`. Per [CLAUDE.md](../CLAUDE.md), Bash on this repo fails with `Exit code 5` due to spaces in the working directory and Git Bash install paths — but if a session ever does coax `dotnet` to run via Bash, it will write POSIX paths into the assets file.

### How to verify it's this issue

```powershell
Get-Content source\StatisticsParser.Vsix\obj\project.assets.json -TotalCount 5
Test-Path "$env:USERPROFILE\.nuget\packages\xunit.analyzers\1.16.0"
Test-Path "$env:USERPROFILE\.nuget\packages\community.visualstudio.toolkit.analyzers\1.0.533"
```

If the first command shows `/Users/...` (POSIX) and the two `Test-Path` calls return `True`, this is the issue.

### Fix (PowerShell)

Run from the repo root:

1. Delete the `obj/` folders so restore writes fresh assets and clears any stale generated `.props`/`.targets`:

   ```powershell
   Remove-Item -Recurse -Force `
     'source\StatisticsParser.Core\obj', `
     'source\StatisticsParser.Core.Tests\obj', `
     'source\StatisticsParser.Vsix\obj'
   ```

2. Restore the solution from PowerShell:

   ```powershell
   dotnet restore StatisticsParserExtension.sln
   ```

3. Reload the solution in Visual Studio — close it and reopen, or use **Build → Clean Solution** then **Build → Rebuild Solution**. VS caches `project.assets.json` in memory for IntelliSense; a reload forces it to re-read.

4. Optional sanity check before reopening VS:

   ```powershell
   dotnet build StatisticsParserExtension.sln
   dotnet test source\StatisticsParser.Core.Tests
   ```

### Why delete `obj/` first instead of `dotnet restore --force`

`Microsoft.VSSDK.BuildTools` writes generated `.props`/`.targets` under `obj/` that bake in `$(NuGetPackageRoot)`. `--force` rewrites `project.assets.json` but leaves those generated files untouched. Cleaning `obj/` rules out half-stale state for free.

### Prevention

If a future session has to invoke `dotnet` from a POSIX-style shell on Windows, set `NUGET_PACKAGES` first so the assets file gets a Windows path even when `HOME` is POSIX:

```powershell
$env:NUGET_PACKAGES = "$env:USERPROFILE\.nuget\packages\"
```

Otherwise stick to PowerShell as [CLAUDE.md](../CLAUDE.md) already advises.
