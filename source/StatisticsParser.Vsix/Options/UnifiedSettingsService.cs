using System.Runtime.InteropServices;

namespace StatisticsParser.Vsix.Options
{
    // Local stub for the VS service identifier whose real declaration lives in
    // Microsoft.Internal.VisualStudio.Interop — that assembly is shipped only via
    // VSSDK BuildTools' tools folder, not as a referenceable lib, so we declare the
    // marker locally with the same Guid. IServiceProvider.GetService(typeof(...)) keys
    // off Type.GUID, so any type bearing this GuidAttribute resolves to the same
    // VS-registered SVsUnifiedSettingsManager service at runtime.
    [Guid("e3684f31-344e-42ea-9047-b620fdc7ac25")]
    internal interface SVsUnifiedSettingsManager
    {
    }
}
