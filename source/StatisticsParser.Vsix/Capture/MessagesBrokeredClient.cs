using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Shell;

namespace StatisticsParser.Vsix.Capture
{
    // Encapsulates all reflection against the SSMS-shipped BrokeredContracts surface so the rest of
    // the codebase deals with strongly-typed primitives (string / int / MessagesSegment / bool) only.
    internal sealed class MessagesBrokeredClient : IDisposable
    {
        private readonly ContractTypes _types;
        private readonly object _proxy;

        private MessagesBrokeredClient(ContractTypes types, object proxy)
        {
            _types = types;
            _proxy = proxy;
        }

        public static async Task<MessagesBrokeredClient> CreateAsync(AsyncPackage package, CancellationToken ct)
        {
            if (package == null) throw new ArgumentNullException(nameof(package));

            var types = await ContractTypes.GetAsync(ct).ConfigureAwait(true);

            await package.JoinableTaskFactory.SwitchToMainThreadAsync(ct);

            var sbcType = ResolveType("Microsoft.VisualStudio.Shell.ServiceBroker.SVsBrokeredServiceContainer");
            if (sbcType == null)
                throw new InvalidOperationException("SVsBrokeredServiceContainer type not loaded.");

            var container = await package.GetServiceAsync(sbcType).ConfigureAwait(true);
            if (container == null)
                throw new InvalidOperationException("SVsBrokeredServiceContainer service unavailable.");

            await package.JoinableTaskFactory.SwitchToMainThreadAsync(ct);

            var getBroker = container.GetType().GetMethod("GetFullAccessServiceBroker");
            if (getBroker == null)
                throw new InvalidOperationException("GetFullAccessServiceBroker not found on container.");

            var broker = getBroker.Invoke(container, null);
            if (broker == null)
                throw new InvalidOperationException("Service broker is null.");

            var proxy = await GetProxyAsync(broker, types, ct).ConfigureAwait(true);
            if (proxy == null)
                throw new InvalidOperationException(
                    "IServiceBroker.GetProxyAsync<IQueryEditorTabDataServiceBrokered>() returned null. " +
                    "Ensure a SQL query window is the active document.");

            return new MessagesBrokeredClient(types, proxy);
        }

        public async Task<bool> IsMessagesPaneAvailableAsync(CancellationToken ct)
        {
            var task = _types.GetAvailablePanesAsyncMethod.Invoke(_proxy, new object[] { ct });
            var panes = await UnwrapAsync(task).ConfigureAwait(true);
            if (!(panes is IEnumerable enumerable)) return false;

            foreach (var info in enumerable)
            {
                if (info == null) continue;
                var paneType = _types.QueryResultsPaneInfo_PaneType.GetValue(info);
                if (Equals(paneType, _types.QueryResultsPane_Messages))
                    return true;
            }
            return false;
        }

        public async Task<MessagesSegment> GetMessagesSegmentAsync(int start, int max, CancellationToken ct)
        {
            var task = _types.GetMessagesTabSegmentAsyncMethod.Invoke(_proxy, new object[] { start, max, ct });
            var segment = await UnwrapAsync(task).ConfigureAwait(true)
                ?? throw new InvalidOperationException("GetMessagesTabSegmentAsync returned null.");

            return new MessagesSegment(
                content: (string)_types.TextContentSegment_Content.GetValue(segment) ?? string.Empty,
                startPosition: (int)_types.TextContentSegment_StartPosition.GetValue(segment),
                totalLength: (int)_types.TextContentSegment_TotalLength.GetValue(segment));
        }

        public void Dispose()
        {
            try { (_proxy as IDisposable)?.Dispose(); }
            catch { /* best-effort */ }
        }

        // The brokered proxy methods return ValueTask<T> (or sometimes Task<T> on the implementation);
        // unwrap either via reflection so call sites get the unboxed result.
        private static async Task<object> UnwrapAsync(object taskOrValueTask)
        {
            if (taskOrValueTask == null) return null;
            if (taskOrValueTask is Task t)
            {
                await t.ConfigureAwait(true);
                return t.GetType().GetProperty("Result")?.GetValue(t);
            }
            var asTask = taskOrValueTask.GetType().GetMethod("AsTask", Type.EmptyTypes);
            if (asTask == null)
                throw new InvalidOperationException(
                    "Cannot await result of type " + taskOrValueTask.GetType().FullName);
            var task = (Task)asTask.Invoke(taskOrValueTask, null);
            await task.ConfigureAwait(true);
            return task.GetType().GetProperty("Result")?.GetValue(task);
        }

        private static async Task<object> GetProxyAsync(object broker, ContractTypes types, CancellationToken ct)
        {
            // The static moniker may be a ServiceMoniker or a ServiceRpcDescriptor (which wraps one).
            // Peel the descriptor down to its Moniker so we can match the IServiceBroker overload.
            object monikerObj = types.QueryEditorTabDataServiceMoniker;
            if (monikerObj.GetType().Name.IndexOf("ServiceRpcDescriptor", StringComparison.Ordinal) >= 0)
            {
                var monikerProp = monikerObj.GetType().GetProperty("Moniker");
                var inner = monikerProp?.GetValue(monikerObj);
                if (inner != null) monikerObj = inner;
            }

            var generics = broker.GetType().GetMethods()
                .Where(m => m.Name == "GetProxyAsync" && m.IsGenericMethodDefinition)
                .ToList();

            // Prefer the overload whose first parameter is assignable from our moniker's runtime type.
            MethodInfo chosen = generics.FirstOrDefault(m =>
            {
                var ps = m.GetParameters();
                return ps.Length >= 1 && ps[0].ParameterType.IsAssignableFrom(monikerObj.GetType());
            }) ?? generics.FirstOrDefault();

            if (chosen == null)
                throw new InvalidOperationException("IServiceBroker.GetProxyAsync<T> not found.");

            var generic = chosen.MakeGenericMethod(types.IQueryEditorTabDataServiceBrokered);
            var ps2 = generic.GetParameters();
            var args = new object[ps2.Length];
            args[0] = monikerObj;
            for (int i = 1; i < ps2.Length; i++)
            {
                if (ps2[i].ParameterType == typeof(CancellationToken))
                    args[i] = ct;
                else if (ps2[i].HasDefaultValue)
                    args[i] = ps2[i].DefaultValue;
                else
                    args[i] = ps2[i].ParameterType.IsValueType
                        ? Activator.CreateInstance(ps2[i].ParameterType)
                        : null;
            }

            var result = generic.Invoke(broker, args);
            return await UnwrapAsync(result).ConfigureAwait(true);
        }

        private static Type ResolveType(string fullName)
        {
            var t = Type.GetType(fullName);
            if (t != null) return t;
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                t = asm.GetType(fullName);
                if (t != null) return t;
            }
            return null;
        }
    }

    internal readonly struct MessagesSegment
    {
        public string Content { get; }
        public int StartPosition { get; }
        public int TotalLength { get; }

        public MessagesSegment(string content, int startPosition, int totalLength)
        {
            Content = content ?? string.Empty;
            StartPosition = startPosition;
            TotalLength = totalLength;
        }
    }
}
