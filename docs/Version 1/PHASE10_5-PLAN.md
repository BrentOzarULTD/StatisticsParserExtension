# Phase 10.5 — Options Page (Unified Settings)

## Goal

Replace the interim `[ProvideOptionPage]` / `UIElementDialogPage` work shipped during the Phase 10 timeframe with the SSMS 22 / VS 2022 **Unified Settings** registration. Result: a top-level **"Statistics Parser"** node in Tools > Options' "All Settings" search UI, with no legacy dialog and no click-through link to one.

The `BaseOptionModel<StatisticsParserOptions>` persistence layer stays — the JSON manifest's migration blocks point at the same `SettingsManager` store keys, so existing read sites (`StatisticsParserOptions.Instance.X`) keep working without code changes.

## Settings to register

| Moniker | Type | Default | UI label |
|---|---|---|---|
| `statisticsParser.completionTime.convertToLocalTime` | bool | `true` | Convert Completion Time to local time |
| `statisticsParser.tempTableNames.mode` | enum string (`doNotChange`, `shorten`) | `shorten` | Temp Table Names |

## Files

**New:**
- `source/StatisticsParser.Vsix/UnifiedSettings/registration.json` — manifest, schema `https://aka.ms/unified-settings-experience/registration/schema`. Sample below.

**Edited:**
- [source/StatisticsParser.Vsix/StatisticsParserPackage.cs](../source/StatisticsParser.Vsix/StatisticsParserPackage.cs) — replace `[ProvideOptionPage(...)]` with `[ProvideSettingsManifest(PackageRelativeManifestFile = @"UnifiedSettings\registration.json")]` (attribute lives in `Microsoft.VisualStudio.Shell.15.0` 17.14.40264, already referenced).
- [source/StatisticsParser.Vsix/StatisticsParser.Vsix.csproj](../source/StatisticsParser.Vsix/StatisticsParser.Vsix.csproj) — add `<Content Include="UnifiedSettings\registration.json"><IncludeInVSIX>true</IncludeInVSIX><CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory></Content>`. Remove the `<Compile>`/`<Page>` entries for `OptionsDialogPage.cs` and `OptionsView.xaml`/`.xaml.cs`.
- [source/StatisticsParser.Vsix/Controls/StatisticsParserControl.xaml.cs](../source/StatisticsParser.Vsix/Controls/StatisticsParserControl.xaml.cs) — replace the `StatisticsParserOptions.Saved` subscription with `ISettingsReader.SubscribeToChanges(...)` over the two monikers; the change handler calls `StatisticsParserOptions.Instance.Load()` to refresh the cached singleton, then re-renders.

**Deleted:**
- `source/StatisticsParser.Vsix/Options/OptionsDialogPage.cs`
- `source/StatisticsParser.Vsix/Options/OptionsView.xaml` + `.xaml.cs`

`StatisticsParserOptions.cs` stays — it's still the read-side model and still backs the Toolkit `SettingsManager` store that the migration block points at. `StatisticsViewBuilder.BuildCompletion` and `IoRowDisplay.FormatTableName` need no changes.

## `registration.json`

```json
{
  "$schema": "https://aka.ms/unified-settings-experience/registration/schema",
  "properties": {
    "statisticsParser.completionTime.convertToLocalTime": {
      "type": "boolean",
      "title": "Convert Completion Time to local time",
      "description": "Show 'Completion Time' lines from the Messages tab in the local time zone instead of the server's reported value.",
      "default": true,
      "migration": {
        "pass": {
          "input": {
            "store": "SettingsManager",
            "path": "StatisticsParser.Vsix.Options.StatisticsParserOptions.ConvertCompletionTimeToLocalTime"
          }
        }
      }
    },
    "statisticsParser.tempTableNames.mode": {
      "type": "string",
      "title": "Temp Table Names",
      "description": "How to render temp table names in the Parse Statistics IO grid.",
      "default": "shorten",
      "enum": [ "doNotChange", "shorten" ],
      "enumItemLabels": [
        "Do not change names",
        "Shorten names"
      ],
      "migration": {
        "enumIntegerToString": {
          "input": {
            "store": "SettingsManager",
            "path": "StatisticsParser.Vsix.Options.StatisticsParserOptions.TempTableNames"
          },
          "map": [
            { "result": "doNotChange", "match": 0 },
            { "result": "shorten",     "match": 1 }
          ]
        }
      }
    }
  },
  "categories": {
    "statisticsParser": {
      "title": "Statistics Parser",
      "description": "Parses STATISTICS IO / STATISTICS TIME output from the Messages tab."
    },
    "statisticsParser.completionTime": { "title": "Completion Time" },
    "statisticsParser.tempTableNames":  { "title": "Temp Tables" }
  }
}
```

Do **not** add `legacyOptionPageId` — that field renders the node as a click-through to the old `DialogPage`, which is exactly the behavior we're removing.

## Live update wiring

```csharp
// StatisticsParserControl.xaml.cs
using Microsoft.VisualStudio.Utilities.UnifiedSettings;

private IDisposable _settingsSubscription;

private void OnLoaded(object sender, RoutedEventArgs e)
{
    ThreadHelper.JoinableTaskFactory.RunAsync(async () =>
    {
        var manager = await VS.Services.GetServiceAsync<SVsUnifiedSettingsManager, ISettingsManager>();
        var reader  = manager.GetReader("StatisticsParser");
        _settingsSubscription = reader.SubscribeToChanges(OnSettingsChanged,
            "statisticsParser.completionTime.convertToLocalTime",
            "statisticsParser.tempTableNames.mode");
    }).FileAndForget("StatisticsParser/SubscribeUnified");
}

private void OnUnloaded(object sender, RoutedEventArgs e)
{
    _settingsSubscription?.Dispose();
    _settingsSubscription = null;
}

private void OnSettingsChanged(SettingsUpdate update)
{
    StatisticsParserOptions.Instance.Load();   // re-pull from SettingsManager
    if (_lastParsed == null) return;
    ThreadHelper.JoinableTaskFactory.RunAsync(async () =>
    {
        await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
        if (_lastParsed != null) Render(_lastParsed);
    }).FileAndForget("StatisticsParser/OnSettingsChanged");
}
```

Service id `SVsUnifiedSettingsManager` and the `SubscribeToChanges` / `GetReader` signatures are documented in `Microsoft.VisualStudio.Utilities.xml` 17.14.40264 lines 5654-5750.

## Pkgdef shape (auto-generated, included for verification)

```
[$RootKey$\SettingsManifests\{0F240EE5-54A7-43CB-9710-3A8E2DEA5B46}]
@="StatisticsParserPackage"
"ManifestPath"="$PackageFolder$\UnifiedSettings\registration.json"
```

Mirrors the working precedent at `C:\Program Files\Microsoft SQL Server Management Studio 22\Release\Common7\IDE\Extensions\Application\Microsoft.SqlServer.Management.HadrTasks.pkgdef` (lines 12–14, paired with `Hadr.registration.json`).

## Verification

1. `dotnet test source/StatisticsParser.Core.Tests` still green (no Core change).
2. `msbuild source/StatisticsParser.Vsix /p:Configuration=Release /p:Platform=x64` builds; the produced VSIX contains `UnifiedSettings\registration.json` at root, and the generated `.pkgdef` includes the `SettingsManifests` key shown above.
3. Reinstall via the standard cycle (`VSIXInstaller /uninstall:0F240EE5-…` → `VSIXInstaller <vsix>` → `SSMS.exe /UpdateConfiguration /RootSuffix Exp`).
4. SSMS Exp → Tools > Options → All Settings → search "Statistics Parser" — top-level node appears with both settings; defaults match the table above.
5. Toggle "Convert Completion Time to local time" → click OK → an open Parse Statistics tab re-renders without re-running the query (completion line shows the source-reported offset).
6. Switch dropdown to "Do not change names" → IO grid shows full underscore-laden temp names with no truncation tooltip.
7. Confirm there is **no** "SQL Server Tools > Statistics Parser" entry under the classic Tools > Options tree.
8. Close + relaunch SSMS Exp → settings persist (reads via `StatisticsParserOptions.Instance` resolve through the same `SettingsManager` store).

## Risks

| Risk | Confidence | Mitigation |
|---|---|---|
| `[ProvideSettingsManifest]` writes the right pkgdef shape | **Confirmed** — Hadr precedent + SDK XML doc. | — |
| SSMS 22 surfaces the manifest as a top-level All-Settings node | **Confirmed by precedent** — Hadr's `sqlServerAlwaysOn` does this. | — |
| Schema URL `https://aka.ms/unified-settings-experience/registration/schema` is stable | **Confirmed** — `$id` of the schema shipped at `<SSMS>\Common7\IDE\UnifiedSettings\registration.schema.json`. | — |
| `BaseOptionModel<T>.Load()` is callable post-construction to refresh the singleton | **Likely** — Toolkit XML doc lists `Load()` as public. | If protected/missing in 17.0.533, expose via a thin static wrapper that calls `LoadAsync().Wait()` on UI thread, or replace `Instance` with a per-call `GetLiveInstanceAsync()` resolution in the change handler. |
| `<Asset Type="Microsoft.VisualStudio.VsPackage.SettingsManifest">` in vsixmanifest is the correct token | **Guess** — not in SDK XML docs we have on disk. | Skip the asset entry entirely; `<Content IncludeInVSIX="true">` + `[ProvideSettingsManifest]` together unambiguously deploy the JSON to `$PackageFolder$\UnifiedSettings\registration.json` and write the pkgdef key. Add the `<Asset>` line only if the deployed VSIX is missing the JSON at `$PackageFolder$`. |

## References

- `C:\Program Files\Microsoft SQL Server Management Studio 22\Release\Common7\IDE\UnifiedSettings\registration.schema.json` — canonical JSON schema.
- `C:\Program Files\Microsoft SQL Server Management Studio 22\Release\Common7\IDE\Extensions\Application\Hadr.registration.json` + `Microsoft.SqlServer.Management.HadrTasks.pkgdef` — the working SSMS-22-shipping example we mirror.
- `C:\Users\richie\.nuget\packages\microsoft.visualstudio.shell.15.0\17.14.40264\lib\net472\Microsoft.VisualStudio.Shell.15.0.xml` — `ProvideSettingsManifestAttribute` doc.
- `C:\Users\richie\.nuget\packages\microsoft.visualstudio.utilities\17.14.40264\lib\net472\Microsoft.VisualStudio.Utilities.xml` — `ISettingsManager` / `ISettingsReader` / `SubscribeToChanges` / `SettingsUpdate` docs.
- [ProvideSettingsManifestAttribute on Microsoft Learn](https://learn.microsoft.com/en-us/dotnet/api/microsoft.visualstudio.shell.providesettingsmanifestattribute?view=visualstudiosdk-2022)
- [Unified Settings — Visual Studio blog](https://devblogs.microsoft.com/visualstudio/unifiedsettings/)
