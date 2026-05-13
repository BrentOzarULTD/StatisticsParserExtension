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
        private readonly string _editorMoniker;

        private MessagesBrokeredClient(ContractTypes types, object proxy, string editorMoniker)
        {
            _types = types;
            _proxy = proxy;
            _editorMoniker = editorMoniker;
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

            // First hop: ISqlEditorServiceBrokered → GetCurrentConnectionAsync → EditorMoniker string.
            // The proxy is only needed for this lookup; dispose immediately even on failure.
            string moniker;
            var editorServiceProxy = await GetProxyAsync(
                broker, types.ISqlEditorServiceBrokered, types.SqlEditorServiceMoniker, ct).ConfigureAwait(true);
            if (editorServiceProxy == null)
                throw new NoActiveEditorException(
                    "IServiceBroker.GetProxyAsync<ISqlEditorServiceBrokered>() returned null.");
            try
            {
                moniker = await GetCurrentEditorMonikerAsync(editorServiceProxy, types, ct).ConfigureAwait(true);
            }
            finally
            {
                (editorServiceProxy as IDisposable)?.Dispose();
            }

            if (string.IsNullOrEmpty(moniker))
                throw new NoActiveEditorException("No active SQL editor window (EditorMoniker is empty).");

            // Second hop: IQueryEditorTabDataServiceBrokered, parameterized by the moniker above.
            var tabDataProxy = await GetProxyAsync(
                broker, types.IQueryEditorTabDataServiceBrokered, types.QueryEditorTabDataServiceMoniker, ct).ConfigureAwait(true);
            if (tabDataProxy == null)
                throw new InvalidOperationException(
                    "IServiceBroker.GetProxyAsync<IQueryEditorTabDataServiceBrokered>() returned null. " +
                    "Ensure a SQL query window is the active document.");

            return new MessagesBrokeredClient(types, tabDataProxy, moniker);
        }

        public async Task<bool> IsMessagesPaneAvailableAsync(CancellationToken ct)
        {
            var args = BuildArgs(_types.GetAvailablePanesAsyncMethod, new object[] { _editorMoniker }, ct);
            var task = _types.GetAvailablePanesAsyncMethod.Invoke(_proxy, args);
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
            var args = BuildArgs(_types.GetMessagesTabSegmentAsyncMethod, new object[] { _editorMoniker, start, max }, ct);
            var task = _types.GetMessagesTabSegmentAsyncMethod.Invoke(_proxy, args);
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

        // Invoke ISqlEditorServiceBrokered.GetCurrentConnectionAsync and pull EditorMoniker off the
        // returned SqlEditorConnectionDetails. Empty/null moniker means there is no active SQL editor.
        private static async Task<string> GetCurrentEditorMonikerAsync(
            object editorServiceProxy, ContractTypes types, CancellationToken ct)
        {
            var args = BuildArgs(types.GetCurrentConnectionAsyncMethod, Array.Empty<object>(), ct);
            object task;
            try
            {
                task = types.GetCurrentConnectionAsyncMethod.Invoke(editorServiceProxy, args);
            }
            catch (TargetInvocationException tie) when (tie.InnerException != null)
            {
                throw new NoActiveEditorException(
                    "GetCurrentConnectionAsync threw " + tie.InnerException.GetType().Name + ": " +
                    tie.InnerException.Message, tie.InnerException);
            }

            object details;
            try
            {
                details = await UnwrapAsync(task).ConfigureAwait(true);
            }
            catch (Exception ex)
            {
                throw new NoActiveEditorException(
                    "GetCurrentConnectionAsync RPC failed: " + ex.GetType().Name + ": " + ex.Message, ex);
            }

            if (details == null) return null;
            return (string)types.SqlEditorConnectionDetails_EditorMoniker.GetValue(details);
        }

        // Build an args array sized to the resolved brokered method's parameter list: copy our
        // known positional args (moniker/start/max) into the leading slots, slot the CancellationToken
        // by type, and fill any trailing parameters with their default value (or a zero-init value
        // type fallback). Mirrors the arg-filling loop in GetProxyAsync below.
        private static object[] BuildArgs(MethodInfo method, object[] positional, CancellationToken ct)
        {
            var ps = method.GetParameters();
            var args = new object[ps.Length];
            int n = Math.Min(positional.Length, ps.Length);
            for (int i = 0; i < n; i++) args[i] = positional[i];
            for (int i = n; i < ps.Length; i++)
            {
                if (ps[i].ParameterType == typeof(CancellationToken)) args[i] = ct;
                else if (ps[i].HasDefaultValue) args[i] = ps[i].DefaultValue;
                else args[i] = ps[i].ParameterType.IsValueType
                    ? Activator.CreateInstance(ps[i].ParameterType)
                    : null;
            }
            return args;
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

        private static async Task<object> GetProxyAsync(
            object broker, Type contractType, object monikerObj, CancellationToken ct)
        {
            // The static moniker may be a ServiceMoniker or a ServiceRpcDescriptor (which wraps one).
            // Peel the descriptor down to its Moniker so we can match the IServiceBroker overload.
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

            var generic = chosen.MakeGenericMethod(contractType);
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

    // Sentinel raised inside CreateAsync when no SQL editor is active or the moniker lookup fails.
    // MessagesTabReader catches this specifically and maps to MessagesCaptureStatus.NoActiveWindow.
    internal sealed class NoActiveEditorException : Exception
    {
        public NoActiveEditorException(string message) : base(message) { }
        public NoActiveEditorException(string message, Exception inner) : base(message, inner) { }
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
