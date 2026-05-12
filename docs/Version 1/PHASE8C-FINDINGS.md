# Phase 8c Findings — Right-click placement

This appendix records what shipped in Phase 8c, the discovery work that bounded the original goal, the latent Phase 7 bug that surfaced and was fixed in passing, and the deferred work for the Messages-tab right-click menu.

## What shipped

**Modified**:

| File | Change |
|---|---|
| [VSCommandTable.vsct](../source/StatisticsParser.Vsix/VSCommandTable.vsct) | Added `<GuidSymbol name="queryWindowContextCommandSet" value="{33F13AC3-80BB-4ECB-85BC-225435603A5E}">` with `<IDSymbol name="queryWindowContextMenu" value="0x0050" />`, and a second `<Group>` declaration with the same `(guid, id)` as `ParseStatisticsGroup` parented to `queryWindowContextMenu` (the SqlFormatter two-Group pattern — vsct compiler accepts duplicate `(guid, id)` Group entries when each declares a distinct `<Parent>`). The original `IDM_VS_CTXT_CODEWIN` group declaration is kept (zero-cost forward-compat for non-SSMS hosts). The button placement rides on the group, so no Button-targeted CommandPlacement is needed for the right-click. Discovery scaffolding (`cmdidDumpMenuCapture` button, Tools-menu placement, IDSymbol) removed. |
| [VSCommandTable.cs](../source/StatisticsParser.Vsix/VSCommandTable.cs) | Added `queryWindowContextCommandSetString` / `queryWindowContextCommandSet` constants in `PackageGuids` and `queryWindowContextMenu = 0x0050` in `PackageIds`. Removed `cmdidDumpMenuCapture`. |

**Deleted**: `Commands/DumpMenuCaptureCommand.cs` (the diagnostic dump trigger).

**Kept dormant**: [Diagnostics/MenuGuidCapture.cs](../source/StatisticsParser.Vsix/Diagnostics/MenuGuidCapture.cs) and its registration at [StatisticsParserPackage.cs:22](../source/StatisticsParser.Vsix/StatisticsParserPackage.cs#L22). The cost is one dictionary insert per IDE-wide `QueryStatus` poll. If a future menu-discovery question arises, the capture is already running and only needs a new dump trigger. Removing it later is a one-line change.

## Behavior delivered

- **Right-click in the .sql query body** in SSMS 22 → `Parse Statistics` appears on the menu, opens the docked **Stats Parser** tool window, captures the active window's Messages tab via the Phase 8b brokered-services pipeline, parses, and renders the same status-line + monospace text as the Tools-menu invocation.
- **Tools → Parse Statistics** unchanged — both entry points reach the same `ExecuteAsync` path.
- The `IDM_VS_CTXT_CODEWIN` placement is retained but inert under SSMS 22; it remains in case a future host honors it.

## The .sql query window IDM — sourced from SqlFormatter

The SSMS 22 .sql query editor (`SqlScriptEditorControl`) does **not** bind to the standard VS shell `IDM_VS_CTXT_CODEWIN`. Phase 7's verification claim that `Parse Statistics` appeared on right-click in the .sql query window was incorrect; the placement was technically valid VSCT but landed on a menu SSMS 22 doesn't surface. This is corrected by [PLAN.md](PLAN.md) §7's verification line.

The correct target is `(33F13AC3-80BB-4ECB-85BC-225435603A5E, 0x0050)`, taken verbatim from the open-source [SqlFormatter](https://github.com/madskristensen/SqlFormatter) extension's [src/VSCommandTable.vsct](https://github.com/madskristensen/SqlFormatter/blob/master/src/VSCommandTable.vsct). SqlFormatter ships against SSMS and has confirmed-working right-click placement; we adopt the same target. The Guid name is `queryWindowContextCommandSet`, the menu ID is `queryWindowContextMenu`.

## Smoke-test results (post-fix)

Verified against a live SSMS 22 experimental hive (`/RootSuffix Exp`). All three [PHASE8B-FINDINGS.md §Smoke-test results](PHASE8B-FINDINGS.md) scenarios were re-run by **right-clicking in the .sql query body** and selecting `Parse Statistics`:

1. **Single statement** (`SET STATISTICS IO, TIME ON; SELECT TOP 100 * FROM sys.objects;`) — captured-text length and parsed row count match the Tools-menu invocation byte-for-byte.
2. **Multi-statement batch** (3× `SELECT TOP 200`) — 64 KB segment paging works, ordering preserved, all `SQL Server Execution Times` blocks captured.
3. **Error path** (`SELECT * FROM dbo.NoSuchTable;`) — `MessagesCaptureStatus.Ok` (the SQL-level error message is still a "message" on the SQL side), `Msg 208` text captured, error renders.

Verified live by user against SSMS 22 Exp on 2026-05-05 — `Parse Statistics` appears on .sql query body right-click; `Tools → Parse Statistics` regression-clean. The behavior is byte-identical to Phase 8b because both entry points reach the same `ExecuteAsync` path.

**Caveat — Phase 8c first-attempt drift**: the placement that originally landed in commit 024080a was a Button-targeted `<CommandPlacement>` parented directly to `queryWindowContextMenu`. That is structurally invalid (the vsct runtime expects `menu → group → button`; a Button placed directly under a Menu is silently dropped). The right-click menu was therefore inert until 2026-05-05, when the placement was rewritten to the SqlFormatter two-`<Group>` pattern documented above. The Phase 8b verification of `Tools → Parse Statistics` was unaffected — that placement (button → `IDG_VS_TOOLS_EXT_TOOLS`, button → group) was always valid.

## Discovery work for the Messages-tab menu — what was tried and why it stopped

The original Phase 8c plan was to also add a `<CommandPlacement>` on the **Messages-tab** right-click menu. Two discovery passes proved the menu exists but failed to recover its `(Guid, Id)`.

### Pass 1 — single right-click

`Tools → Dump Menu Capture` reset → 1 right-click on Messages tab → `Tools → Dump Menu Capture` flush. 348 unique `(cmdGroup, cmdId)` pairs, dominated by background polling. The 2-fire band turned out to be `cmdidExternalCommand1`–`24` (`guidVSStd97 0x0276`–`0x028E`) polled twice as the Tools menu opened twice; not the Messages-tab content.

### Pass 2 — five right-clicks

Same procedure but with 5 right-clicks before the second flush. This time a **clean 5-fire band** of exactly 17 commands appeared, distinct from all other firing counts:

| Source | Cmd IDs (hex) |
|---|---|
| `5efc7975-14bc-11cf-9b2b-00aa00573819` (`guidVSStd97`) | `0x000F`, `0x00A8`, `0x00E3`, `0x00E4` |
| `732abe75-cd80-11d0-a2db-00aa00a3efff` (`guidStdEditor`) | `0x000C`, `0x002B` |
| `52692960-56bc-4989-b5d3-94c47a513e8d` (Command Explorer: owns `File.SaveResultsAs`; appears in MenuBar, SQL Results Grid Tab Context, SQL Results Message Tab Context) | `0x0065`, `0x0066`, `0x0067`, `0x012C` |
| `160961b3-909d-4b28-9353-a1bef587b4a6` (VS Search package, per `Microsoft.VisualStudio.Search.pkgdef`) | `0x0021` |
| `e4b9bb05-1963-4774-8cfc-518359e3fce3` | `0x2F01`, `0x2F02` |
| `d63db1f0-404e-4b21-9648-ca8d99245ec3` | `0x0029` |
| `22949936-c754-46bd-9cdb-cb4bfa688e18` | `0x0030` |
| `4a79114a-19e4-11d3-b86b-00c04f79f802` (`Msenv.Core.Pkgdef`) | `0x0121` |
| `15061d55-e726-4e3c-97d3-1b871d9b5ae9` | `0x7002` |

**This is the canonical fingerprint of the Messages-tab right-click menu.** Anything that places these 17 commands on the same menu *is* that menu. Future investigators can use this fingerprint to cross-reference Command Explorer or any other tooling.

### Why the IDM cannot be recovered from this fingerprint alone

`IOleCommandTarget.QueryStatus` is invoked for **commands inside a menu**, never for the menu's own IDM. Command IDs and menu IDs share a `(Guid, Id)` namespace but are different kinds of entries; menus are never polled by `QueryStatus`. So no amount of right-click probing can surface the menu's identity through this method.

### Other paths that failed

- **Targeted byte/text scans** of `SQLEditors.dll`, `Microsoft.SqlServer.Management.UI.VSIntegration.SqlEditor.BrokeredContracts.dll`, `SqlScriptoriaCommon.dll`, `Microsoft.SqlServer.Management.SSMSAgent.dll`, and `Microsoft.SqlServer.Management.SqlStudio.Actions.dll` for the candidate Guids (both as little-endian binary and as ASCII text) — **zero hits**. The Guids likely live packed (resource-compressed) in the binaries or in a VS-shell DLL outside the SQL-specific tree. A full scan of all 7218 DLLs under SSMS 22's IDE root was attempted but stopped early — too slow to complete without a more selective bytewise pre-filter.
- **Command Explorer browsing**. The user manually browsed Command Explorer and could not find a menu that exposes its own (Guid, Id) for the Messages-tab right-click. Command Explorer in this build does not support filter-by-Guid on menus, only on commands. The `52692960-...` finding (which Command Explorer surfaced for `File.SaveResultsAs`) is the command set, not a menu Guid — useful as a fingerprint cross-reference but not as a placement target.

### Why the standard VS context-menu IDMs aren't viable guesses

Empirical guessing (e.g. `IDM_VS_CTXT_OUTPUTWINDOW = 0x019C` under `guidSHLMainMenu`) was considered. The Messages tab is **not** an `IVsOutputWindow` pane in SSMS 22 (confirmed in [PHASE7-RESEARCH.md](PHASE7-RESEARCH.md) §Spike 2; the pane is owned by `SQLEditors.dll`'s `SqlScriptEditorControl`, not the VS Output window), so `IDM_VS_CTXT_OUTPUTWINDOW` would be the wrong target by definition. Other shell IDMs (`IDM_VS_CTXT_TASKLIST`, etc.) are even less likely. Any guessing campaign would burn build-deploy cycles without a principled stopping criterion; the bounded effort goes to deferred work below.

## Phase 7 latent-bug discovery — fixed in passing

While verifying the Phase 8c approach, the user confirmed `Parse Statistics` *also* did not appear on right-click in the .sql query body. Phase 7's verification line had claimed it did. The placement was technically valid VSCT (`<Parent guid="guidSHLMainMenu" id="IDM_VS_CTXT_CODEWIN" />`) but lands on a menu SSMS 22 doesn't surface. The SqlFormatter cross-reference fixes this: the SSMS-specific `queryWindowContextMenu` now hosts our command. [PLAN.md](PLAN.md) §7's verification line is updated.

## Deferred — Messages-tab right-click placement

The Messages-tab right-click menu's `(Guid, Id)` remains undiscovered after this session's effort. Empirical evidence (the 5-fire fingerprint above) proves the menu exists and is OLE-routed; only its identity is unknown.

**Suggested future direction**: bypass VSCT entirely by appending a `MenuItem` to the live WPF `ContextMenu` of the messages control via reflection into `SqlScriptEditorControl`. The same reflection pattern used in [Capture/MessagesBrokeredClient.cs](../source/StatisticsParser.Vsix/Capture/MessagesBrokeredClient.cs) for the brokered service applies. Pros: skips the IDM-discovery problem completely. Cons: fragile across SSMS minor versions, completely different architecture from VSCT-based placement, and the `MenuItem` would not theme via VS environment colors automatically.

**Alternative future direction**: targeted decompile of `SQLEditors.dll`, `SqlScriptoriaCommon.dll`, and any pkgdef/CTO resources via ILSpy/dotPeek to locate the menu definition. Authoritative but heavy lift.

For now, **Tools → Parse Statistics** and the .sql query body right-click are the canonical entry points; the capture pipeline reads from the active window's Messages tab regardless of where the user invokes from, so functional coverage is complete even without the Messages-tab right-click placement.

## Out of scope

- Structured `ParseResult` rendering with DataGrids (Phase 9).
- Theme bindings (Phase 10).
- CI workflow (Phase 11).
- Messages-tab right-click placement (deferred — see above).
