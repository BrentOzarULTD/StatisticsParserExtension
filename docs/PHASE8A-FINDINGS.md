# Phase 8a Findings — Messages-tab text accessor

This is the §8a "Output of 8a" appendix referenced from [PLAN.md](PLAN.md). It records which surface returns the active SSMS query window's Messages-tab text, the reflection signatures used to invoke it, and what to do when those signatures break across SSMS minor versions.

## Surface chosen

**Brokered service** (PLAN.md §8a Option 1).

The empirical work was done by static introspection of the SSMS 22 install rather than by running the Phase 8a probe scaffolding. The probe code did the dynamic exploration that pointed at this surface; the introspection then confirmed exact signatures without needing a probe round-trip.

| Item | Value |
|---|---|
| Assembly | `Microsoft.SqlServer.Management.UI.VSIntegration.SqlEditor.BrokeredContracts.dll` |
| Version inspected | 22.5.175.63478 |
| Path | `C:\Program Files\Microsoft SQL Server Management Studio 22\Release\Common7\IDE\` |
| Namespace | `Microsoft.SqlServer.Management.UI.VSIntegration.BrokeredServices` |

## Reflection signatures (consumed in [Capture/ContractTypes.cs](../source/StatisticsParser.Vsix/Capture/ContractTypes.cs))

```
interface IQueryEditorTabDataServiceBrokered
{
    ValueTask<TextContentSegment>      GetMessagesTabSegmentAsync(int startPosition, int maxLength, CancellationToken)
    ValueTask<QueryResultsPaneInfo[]>  GetAvailablePanesAsync(CancellationToken)
    // sibling segment getters not consumed: GetTextResultsSegmentAsync, GetGridResultsSegmentAsync,
    //                                       GetQueryPlanXmlSegmentAsync, GetClientStatisticsAsync
}

class TextContentSegment
{
    string Content        { get; }
    int    StartPosition  { get; }
    int    TotalLength    { get; }
}

enum QueryResultsPane { Messages, GridResults, TextResults, QueryPlan, SpatialResults, ClientStatistics }

class QueryResultsPaneInfo
{
    QueryResultsPane PaneType { get; }
    string           Name     { get; }
    bool             IsActive { get; }
    string           Content  { get; }
}

static class QueryEditorTabDataServiceDescriptors
{
    public static readonly /* ServiceMoniker or ServiceRpcDescriptor */ QueryEditorTabDataService;
}
```

The `IServiceBroker` proxy is acquired via `SVsBrokeredServiceContainer.GetFullAccessServiceBroker()` and `IServiceBroker.GetProxyAsync<IQueryEditorTabDataServiceBrokered>(moniker, ..., ct)`. The static `QueryEditorTabDataService` member may be either a `Microsoft.ServiceHub.Framework.ServiceMoniker` or a `ServiceRpcDescriptor` that wraps one; [MessagesBrokeredClient.cs](../source/StatisticsParser.Vsix/Capture/MessagesBrokeredClient.cs) handles both.

## Paging

`GetMessagesTabSegmentAsync(0, 65536, ct)` returns up to 64 KB of text plus the snapshot `TotalLength`. `MessagesTabReader` loops the call advancing `start += segment.Content.Length` until `start >= TotalLength`. The first segment's `TotalLength` is treated as the snapshot — if the underlying tab grows mid-read (e.g. while a query is still executing), only that prefix is captured. An iteration cap of `(TotalLength / 65536) + 4` and an empty-`Content` guard prevent infinite loops.

## Surfacing failures across SSMS minor versions

`ContractTypes.Initialize()` does each `GetType` / `GetMethod` / `GetProperty` lookup with a clear error message naming the missing member. If SSMS ships a renamed type or method, users get a single descriptive `MessagesCaptureStatus.ContractsAssemblyMissing` or `ProxyUnavailable` error in the tool window plus a `WriteFailure` trace in the "Statistics Parser — Diagnostics" output pane — never a `NullReferenceException`.

When SSMS bumps a minor version and the surface changes:

1. Reload the install copy of `BrokeredContracts.dll` and re-enumerate types in the namespace above (e.g. via `Assembly.LoadFile` + `GetExportedTypes`).
2. Update names/parameter shapes in [ContractTypes.cs](../source/StatisticsParser.Vsix/Capture/ContractTypes.cs).
3. If `GetMessagesTabSegmentAsync` itself disappears, fall back through PLAN.md §8a's remaining surfaces (Options 2–4).

## Items not done in 8a

- Messages-tab right-click context-menu `Guid`/`ID` capture is **deferred to a follow-on Phase 8c task**. Phase 7's Tools-menu placement gives users a working entry point. Capturing the parent menu Guid via [Diagnostics/MenuGuidCapture.cs](../source/StatisticsParser.Vsix/Diagnostics/MenuGuidCapture.cs) is best done in a focused SSMS session after 8b proves end-to-end capture works; otherwise we risk capturing a Guid for a code path we haven't yet exercised.
