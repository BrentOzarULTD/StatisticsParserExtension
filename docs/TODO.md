# TODO

## Features

- [ ] **Language auto-detect or explicit selection** — `ParseData` accepts a `ParserLanguage` and `StatisticsParser.Core` ships three instances (English, Spanish, Italian), but the VSIX always passes English. Two open follow-ons: (a) auto-detect by scanning the input for unambiguous markers (e.g., `Tabla`/`Tabella`/`Table` line prefix; `Tiempo de CPU` vs. `tempo di CPU` vs. `CPU time`) with English fallback; or (b) a SSMS Tools > Options page that lets the user pick.
- [ ] **Locale-specific number formats** — Spanish and Italian SQL Server output may emit numbers using `.` thousand separator and `,` decimal separator. The upstream JSON `numberformat` block describes the per-locale rule. The current C# parser assumes invariant integers. Add a `NumberFormat` field to `ParserLanguage` and route IO/time number parsing through a culture-aware helper.
- [ ] **Localized UI display strings** — the Stats Parser tool window renders English column headers (`"Logical Reads"`, `"Totals:"`, `"rows affected"`, etc.) regardless of the parsed input language. Upstream JSON keys `header*`, `totals`, `headerrowsaffected`, `headerrowaffected` cover the localized variants. Surface these via `ParserLanguage` and bind the WPF UI to them.
- [ ] **Compare Statistics Output** - Set a Parse Statistics tab as comparison base, this tab will no longer be updated. Subsquent parses will create two additional tabs. The first new tab will be the output of the messages tab. The second new tab will show the differences between the two.

## Distribution

- [ ] **Visual Studio Marketplace** — publish both VSIX packages to the VS Marketplace so users can install directly from SSMS via Extensions > Manage Extensions.
- [ ] **Chocolatey** — create a Chocolatey package for the extension to support automated/enterprise installs.
- [ ] **winget** — submit a winget manifest to the Windows Package Manager Community Repository.
