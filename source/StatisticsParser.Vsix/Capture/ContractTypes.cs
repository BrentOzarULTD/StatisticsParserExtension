using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Threading;

namespace StatisticsParser.Vsix.Capture
{
    // Late-bound reflection cache for the SSMS-shipped BrokeredContracts assembly. The DLL ships with
    // SSMS and cannot be redistributed by this VSIX, so the assembly is loaded from the SSMS install
    // at runtime and types/methods are looked up by name. Initialization runs once per process; its
    // result (success or failure) is cached in the AsyncLazy<T> wrapper.
    internal sealed class ContractTypes
    {
        public const string ContractsDllName =
            "Microsoft.SqlServer.Management.UI.VSIntegration.SqlEditor.BrokeredContracts.dll";

        private const string Ns = "Microsoft.SqlServer.Management.UI.VSIntegration.BrokeredServices";

        private static readonly AsyncLazy<ContractTypes> _instance =
            new AsyncLazy<ContractTypes>(LoadAsync, joinableTaskFactory: null);

        public static Task<ContractTypes> GetAsync(CancellationToken ct) => _instance.GetValueAsync(ct);

        public Assembly ContractsAssembly { get; private set; }
        public Type IQueryEditorTabDataServiceBrokered { get; private set; }
        public Type TextContentSegmentType { get; private set; }
        public Type QueryResultsPaneType { get; private set; }
        public Type QueryResultsPaneInfoType { get; private set; }
        public object QueryEditorTabDataServiceMoniker { get; private set; }

        public MethodInfo GetMessagesTabSegmentAsyncMethod { get; private set; }
        public MethodInfo GetAvailablePanesAsyncMethod { get; private set; }

        public PropertyInfo TextContentSegment_Content { get; private set; }
        public PropertyInfo TextContentSegment_StartPosition { get; private set; }
        public PropertyInfo TextContentSegment_TotalLength { get; private set; }
        public PropertyInfo QueryResultsPaneInfo_PaneType { get; private set; }
        public object QueryResultsPane_Messages { get; private set; }

        private static Task<ContractTypes> LoadAsync()
        {
            // Initialize() is purely CPU-bound (Assembly.LoadFrom + reflection), so synchronous work
            // wrapped in Task.FromResult is fine here — no main-thread or RPC calls.
            var ct = new ContractTypes();
            ct.Initialize();
            return Task.FromResult(ct);
        }

        private void Initialize()
        {
            string ideDir = AppDomain.CurrentDomain.BaseDirectory;
            string dllPath = Path.Combine(ideDir, ContractsDllName);
            if (!File.Exists(dllPath))
                throw new FileNotFoundException(
                    "SSMS BrokeredContracts assembly not found at " + dllPath, dllPath);

            ContractsAssembly = Assembly.LoadFrom(dllPath);

            IQueryEditorTabDataServiceBrokered = RequireType("IQueryEditorTabDataServiceBrokered");
            TextContentSegmentType = RequireType("TextContentSegment");
            QueryResultsPaneType = RequireType("QueryResultsPane");
            QueryResultsPaneInfoType = RequireType("QueryResultsPaneInfo");
            var descriptorsType = RequireType("QueryEditorTabDataServiceDescriptors");

            GetMessagesTabSegmentAsyncMethod = RequireMethod(
                IQueryEditorTabDataServiceBrokered, "GetMessagesTabSegmentAsync", paramCount: 3);
            GetAvailablePanesAsyncMethod = RequireMethod(
                IQueryEditorTabDataServiceBrokered, "GetAvailablePanesAsync", paramCount: 1);

            TextContentSegment_Content = RequireProperty(TextContentSegmentType, "Content");
            TextContentSegment_StartPosition = RequireProperty(TextContentSegmentType, "StartPosition");
            TextContentSegment_TotalLength = RequireProperty(TextContentSegmentType, "TotalLength");
            QueryResultsPaneInfo_PaneType = RequireProperty(QueryResultsPaneInfoType, "PaneType");

            QueryResultsPane_Messages = Enum.Parse(QueryResultsPaneType, "Messages");
            QueryEditorTabDataServiceMoniker = ResolveMonikerFrom(descriptorsType);
        }

        private Type RequireType(string simpleName)
        {
            var full = Ns + "." + simpleName;
            var t = ContractsAssembly.GetType(full, throwOnError: false);
            if (t == null)
                throw new InvalidOperationException(
                    "BrokeredContracts type not found: " + full +
                    " (SSMS minor version may have moved it)");
            return t;
        }

        private static MethodInfo RequireMethod(Type type, string name, int paramCount)
        {
            var m = type.GetMethods().FirstOrDefault(x => x.Name == name && x.GetParameters().Length == paramCount);
            if (m == null)
                throw new InvalidOperationException(
                    "BrokeredContracts method not found: " + type.FullName + "." + name +
                    " with " + paramCount + " parameters");
            return m;
        }

        private static PropertyInfo RequireProperty(Type type, string name)
        {
            var p = type.GetProperty(name);
            if (p == null)
                throw new InvalidOperationException(
                    "BrokeredContracts property not found: " + type.FullName + "." + name);
            return p;
        }

        private static object ResolveMonikerFrom(Type descriptorsType)
        {
            const BindingFlags bf =
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance;

            // Try the known member name first.
            var prop = descriptorsType.GetProperty("QueryEditorTabDataService", bf);
            if (prop != null && prop.GetGetMethod(true)?.IsStatic == true)
            {
                var v = prop.GetValue(null);
                if (v != null) return v;
            }
            var field = descriptorsType.GetField("QueryEditorTabDataService", bf);
            if (field != null && field.IsStatic)
            {
                var v = field.GetValue(null);
                if (v != null) return v;
            }

            // Fallback: any moniker- or descriptor-shaped static member on the type.
            foreach (var p in descriptorsType.GetProperties(bf))
            {
                if (p.GetGetMethod(true)?.IsStatic != true) continue;
                if (!IsMonikerShape(p.PropertyType)) continue;
                var v = p.GetValue(null);
                if (v != null) return v;
            }
            foreach (var f in descriptorsType.GetFields(bf))
            {
                if (!f.IsStatic) continue;
                if (!IsMonikerShape(f.FieldType)) continue;
                var v = f.GetValue(null);
                if (v != null) return v;
            }

            throw new InvalidOperationException(
                "Could not locate QueryEditorTabDataServiceDescriptors.QueryEditorTabDataService moniker");
        }

        private static bool IsMonikerShape(Type t) =>
            t != null && (t.Name.IndexOf("ServiceMoniker", StringComparison.Ordinal) >= 0
                          || (t.FullName ?? string.Empty).IndexOf("ServiceRpcDescriptor", StringComparison.Ordinal) >= 0);
    }
}
